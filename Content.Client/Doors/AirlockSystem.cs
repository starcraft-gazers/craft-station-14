using Content.Client.Wires.Visualizers;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Prying.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client.Doors;

public sealed class AirlockSystem : SharedAirlockSystem
{
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirlockComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<AirlockComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<AirlockComponent, BeforePryEvent>(OnAirlockPryAttempt);
    }

    private void OnAirlockPryAttempt(EntityUid uid, AirlockComponent component, ref BeforePryEvent args)
    {
        // TODO: Temporary until airlocks predicted.
        args.Cancelled = true;
    }

    private void OnComponentStartup(EntityUid uid, AirlockComponent comp, ComponentStartup args)
    {
        // Has to be on component startup because we don't know what order components initialize in and running this before DoorComponent inits _will_ crash.
        if(!TryComp<DoorComponent>(uid, out var door))
            return;

        if (comp.OpenUnlitVisible) // Otherwise there are flashes of the fallback sprite between clicking on the door and the door closing animation starting.
        {
            door.OpenSpriteStates.Add((DoorVisualLayers.BaseUnlit, comp.OpenSpriteState));
            door.ClosedSpriteStates.Add((DoorVisualLayers.BaseUnlit, comp.ClosedSpriteState));
        }

        ((Animation)door.OpeningAnimation).AnimationTracks.Add(new AnimationTrackSpriteFlick()
            {
                LayerKey = DoorVisualLayers.BaseUnlit,
                KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(comp.OpeningSpriteState, 0f) },
            }
        );

        ((Animation)door.ClosingAnimation).AnimationTracks.Add(new AnimationTrackSpriteFlick()
            {
                LayerKey = DoorVisualLayers.BaseUnlit,
                KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(comp.ClosingSpriteState, 0f) },
            }
        );

        door.DenyingAnimation = new Animation()
        {
            Length = TimeSpan.FromSeconds(comp.DenyAnimationTime),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick()
                {
                    LayerKey = DoorVisualLayers.BaseUnlit,
                    KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(comp.DenySpriteState, 0f) },
                }
            }
        };

        if(!comp.AnimatePanel)
            return;

        ((Animation)door.OpeningAnimation).AnimationTracks.Add(new AnimationTrackSpriteFlick()
        {
            LayerKey = WiresVisualLayers.MaintenancePanel,
            KeyFrames = {new AnimationTrackSpriteFlick.KeyFrame(comp.OpeningPanelSpriteState, 0f)},
        });

        ((Animation)door.ClosingAnimation).AnimationTracks.Add(new AnimationTrackSpriteFlick
        {
            LayerKey = WiresVisualLayers.MaintenancePanel,
            KeyFrames = {new AnimationTrackSpriteFlick.KeyFrame(comp.ClosingPanelSpriteState, 0f)},
        });
    }

    private void OnAppearanceChange(EntityUid uid, AirlockComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var boltedVisible = false;
        var emergencyLightsVisible = false;
        var unlitVisible = false;

        if (!_appearanceSystem.TryGetData<DoorState>(uid, DoorVisuals.State, out var state, args.Component))
            state = DoorState.Closed;

        if (_appearanceSystem.TryGetData<bool>(uid, DoorVisuals.Powered, out var powered, args.Component) && powered)
        {
            boltedVisible = _appearanceSystem.TryGetData<bool>(uid, DoorVisuals.BoltLights, out var lights, args.Component)
                            && lights
                            && state == DoorState.Closed;
            emergencyLightsVisible = _appearanceSystem.TryGetData<bool>(uid, DoorVisuals.EmergencyLights, out var eaLights, args.Component) && eaLights
                            && !boltedVisible;
            unlitVisible =
                    state == DoorState.Closing
                ||  state == DoorState.Opening
                ||  state == DoorState.Denying
                || (state == DoorState.Open && comp.OpenUnlitVisible)
                //|| (_appearanceSystem.TryGetData<bool>(uid, DoorVisuals.ClosedLights, out var closedLights, args.Component) && closedLights);
                || (state == DoorState.Closed && !emergencyLightsVisible && !boltedVisible);
        }

        if (state == DoorState.Closed) // Shitfix to closing animation not cleaning up after state change. I am not suppose to change states here, probably.
            args.Sprite.LayerSetState(DoorVisualLayers.BaseUnlit, "closed_unlit");
        args.Sprite.LayerSetVisible(DoorVisualLayers.BaseUnlit, unlitVisible);
        args.Sprite.LayerSetVisible(DoorVisualLayers.BaseBolted, boltedVisible);
        if (comp.EmergencyAccessLayer)
        {
            args.Sprite.LayerSetVisible(
                DoorVisualLayers.BaseEmergencyAccess,
                    emergencyLightsVisible
                &&  state != DoorState.Open
                &&  state != DoorState.Opening
                &&  state != DoorState.Closing
            );
        }
    }
}
