using System.Threading;
using Content.Server.DoAfter;
using Content.Server.Medical.Components;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.MobState.Components;
using Robust.Server.GameObjects;
using static Content.Shared.MedicalScanner.SharedHealthAnalyzerComponent;

namespace Content.Server.Medical
{
    public sealed class HealthAnalyzerSystem : EntitySystem
    {
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<HealthAnalyzerComponent, ActivateInWorldEvent>(HandleActivateInWorld);
            SubscribeLocalEvent<HealthAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<TargetScanSuccessfulEvent>(OnTargetScanSuccessful);
            SubscribeLocalEvent<ScanCancelledEvent>(OnScanCancelled);
        }

        private void HandleActivateInWorld(EntityUid uid, HealthAnalyzerComponent healthAnalyzer, ActivateInWorldEvent args)
        {
            OpenUserInterface(args.User, healthAnalyzer);
        }

        private void OnAfterInteract(EntityUid uid, HealthAnalyzerComponent healthAnalyzer, AfterInteractEvent args)
        {
            if (healthAnalyzer.CancelToken != null)
            {
                healthAnalyzer.CancelToken.Cancel();
                healthAnalyzer.CancelToken = null;
                return;
            }

            if (args.Target == null)
                return;

            if (!args.CanReach)
                return;

            if (healthAnalyzer.CancelToken != null)
                return;

            if (!HasComp<MobStateComponent>(args.Target))
                return;

            healthAnalyzer.CancelToken = new CancellationTokenSource();
            _doAfterSystem.DoAfter(new DoAfterEventArgs(args.User, healthAnalyzer.ScanDelay, healthAnalyzer.CancelToken.Token, target: args.Target)
            {
                BroadcastFinishedEvent = new TargetScanSuccessfulEvent(args.User, args.Target, healthAnalyzer),
                BroadcastCancelledEvent = new ScanCancelledEvent(healthAnalyzer),
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                BreakOnStun = true,
                NeedHand = true
            });
        }

        private void OnTargetScanSuccessful(TargetScanSuccessfulEvent args)
        {
            args.Component.CancelToken = null;
            UpdateScannedUser(args.Component.Owner, args.User, args.Target, args.Component);
        }

        private void OpenUserInterface(EntityUid user, HealthAnalyzerComponent healthAnalyzer)
        {
            if (!TryComp<ActorComponent>(user, out var actor))
                return;

            healthAnalyzer.UserInterface?.Open(actor.PlayerSession);
        }

        public void UpdateScannedUser(EntityUid uid, EntityUid user, EntityUid? target, HealthAnalyzerComponent? healthAnalyzer)
        {
            if (!Resolve(uid, ref healthAnalyzer))
                return;

            if (target == null || healthAnalyzer.UserInterface == null)
                return;

            if (!HasComp<DamageableComponent>(target))
                return;

            OpenUserInterface(user, healthAnalyzer);
            healthAnalyzer.UserInterface?.SendMessage(new HealthAnalyzerScannedUserMessage(target));
        }

        private static void OnScanCancelled(ScanCancelledEvent args)
        {
            args.HealthAnalyzer.CancelToken = null;
        }

        private sealed class ScanCancelledEvent : EntityEventArgs
        {
            public readonly HealthAnalyzerComponent HealthAnalyzer;
            public ScanCancelledEvent(HealthAnalyzerComponent healthAnalyzer)
            {
                HealthAnalyzer = healthAnalyzer;
            }
        }

        private sealed class TargetScanSuccessfulEvent : EntityEventArgs
        {
            public EntityUid User { get; }
            public EntityUid? Target { get; }
            public HealthAnalyzerComponent Component { get; }

            public TargetScanSuccessfulEvent(EntityUid user, EntityUid? target, HealthAnalyzerComponent component)
            {
                User = user;
                Target = target;
                Component = component;
            }
        }
    }
}
