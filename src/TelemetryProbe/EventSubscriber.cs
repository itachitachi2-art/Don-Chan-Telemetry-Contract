using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DonChan.TelemetryProbe
{
    internal static class EventSubscriber
    {
        private static readonly HashSet<string> Bound = new HashSet<string>(StringComparer.Ordinal);
        private static readonly object Sync = new object();
        private static ProbeConfig _config;

        private static readonly Dictionary<string, string[]> StaticTargets = new Dictionary<string, string[]>
        {
            { "QuestEventManager", new[]{ "AddItem","AssembleItem","BiomeEnter","BlockActivate","BlockChange","BlockDestroy","BlockPickup","BlockPlace","BlockUpgrade","BloodMoonSurvive","BuyItems","ChallengeAwardCredit","ChallengeComplete","ContainerClosed","ContainerOpened","CraftItem","EntityKill","ExchangeFromItem","ExplosionDetected","HarvestItem","HoldItem","NPCInteract","NPCMeet","QuestAwardCredit","QuestComplete","RepairItem","ScrapItem","SellItems","SkillPointSpent","SleeperVolumePositionAdd","SleeperVolumePositionRemove","SleepersCleared","TimeSurvive","UseItem","WearItem","WindowChanged" } },
            { "GameEventManager", new[]{ "GameBlockRemoved","GameBlocksAdded","GameBlocksRemoved","GameEntityDespawned","GameEntityKilled","GameEntitySpawned","GameEventCompleted" } },
            { "GameManager", new[]{ "OnClientSpawned","OnLocalPlayerChanged","OnWorldChanged" } },
            { "GamePrefs", new[]{ "OnGamePrefChanged" } },
            { "GameStats", new[]{ "OnChangedDelegates" } },
            { "CraftingManager", new[]{ "RecipeUnlocked" } }
        };

        public static void Initialize(ProbeConfig config)
        {
            _config = config;
            foreach (var kv in StaticTargets)
            {
                Type type = ReflectionUtil.FindType(kv.Key);
                if (type == null)
                {
                    Capability("static-event-type", kv.Key, false, "type-not-found");
                    continue;
                }
                object singleton = ReflectionUtil.ReadMember(type, "Current", "Instance", "current", "instance");
                foreach (string name in kv.Value)
                {
                    bool staticBound = TryBind(type, null, name, "static");
                    if (!staticBound && singleton != null) TryBind(singleton.GetType(), singleton, name, "singleton");
                }
            }
        }

        public static void RefreshInstanceSubscriptions(SnapshotRoots roots)
        {
            TryBindMany(roots.Player, "player", new[]{ "QuestAccepted","QuestChanged","QuestRemoved","SharedQuestAdded","SharedQuestRemoved","InvitedToParty","PartyChanged","PartyJoined","PartyLeave" });
            TryBindMany(roots.Inventory, "inventory", new[]{ "OnToolbeltItemsChangedInternal" });
            TryBindMany(roots.Bag, "bag", new[]{ "OnBackpackItemsChangedInternal" });
            TryBindMany(roots.Equipment, "equipment", new[]{ "OnChanged","CosmeticUnlocked" });
            TryBindMany(roots.World, "world", new[]{ "EntityLoadedDelegates","EntityUnloadedDelegates","OnWorldChanged" });
        }

        private static void TryBindMany(object instance, string scope, IEnumerable<string> names)
        {
            if (instance == null) return;
            foreach (string name in names) TryBind(instance.GetType(), instance, name, scope);
        }

        private static bool TryBind(Type type, object instance, string memberName, string scope)
        {
            int objectId = instance == null ? 0 : ReflectionUtil.ObjectId(instance);
            string key = type.FullName + "::" + memberName + "::" + objectId;
            lock (Sync) { if (Bound.Contains(key)) return true; }

            try
            {
                BindingFlags flags = instance == null ? ReflectionUtil.AllStatic : ReflectionUtil.AllInstance;
                var ev = type.GetEvent(memberName, flags);
                if (ev != null)
                {
                    Delegate handler = BuildDelegate(ev.EventHandlerType, type.FullName + "." + memberName, scope);
                    ev.AddEventHandler(instance, handler);
                    lock (Sync) Bound.Add(key);
                    Capability("event", type.FullName + "." + memberName, true, "EventInfo;" + scope);
                    return true;
                }

                var field = type.GetField(memberName, flags);
                if (field != null && typeof(Delegate).IsAssignableFrom(field.FieldType))
                {
                    Delegate handler = BuildDelegate(field.FieldType, type.FullName + "." + memberName, scope);
                    Delegate current = field.GetValue(instance) as Delegate;
                    field.SetValue(instance, Delegate.Combine(current, handler));
                    lock (Sync) Bound.Add(key);
                    Capability("event", type.FullName + "." + memberName, true, "delegate-field;" + scope);
                    return true;
                }

                Capability("event", type.FullName + "." + memberName, false, "not-found;" + scope);
                return false;
            }
            catch (Exception ex)
            {
                Capability("event", type.FullName + "." + memberName, false, ex.GetType().Name + ":" + ex.Message);
                return false;
            }
        }

        private static Delegate BuildDelegate(Type delegateType, string eventName, string scope)
        {
            MethodInfo invoke = delegateType.GetMethod("Invoke");
            ParameterInfo[] parameters = invoke.GetParameters();
            var lambdaParams = parameters.Select(p => Expression.Parameter(p.ParameterType, p.Name ?? "arg")).ToArray();
            var boxed = lambdaParams.Select(p => Expression.Convert(p, typeof(object)));
            var argsArray = Expression.NewArrayInit(typeof(object), boxed);
            MethodInfo sink = typeof(EventSubscriber).GetMethod("HandleEvent", BindingFlags.Static | BindingFlags.NonPublic);
            var call = Expression.Call(sink, Expression.Constant(eventName), Expression.Constant(scope), argsArray);
            Expression body = invoke.ReturnType == typeof(void) ? (Expression)call : Expression.Block(call, Expression.Default(invoke.ReturnType));
            return Expression.Lambda(delegateType, body, lambdaParams).Compile();
        }

        private static void HandleEvent(string eventName, string scope, object[] args)
        {
            try
            {
                if (_config.EnableNormalizedEvents && (!_config.SuppressNoisyEvents || EventNormalizer.ShouldEmitDelegate(eventName, args)))
                    ProbeLog.Event(EventNormalizer.NormalizeDelegate(eventName, scope, args));

                if (_config.AuditMode && _config.EnableRawAuditEvents)
                {
                    var inspected = new List<object>();
                    int limit = Math.Min(args == null ? 0 : args.Length, 16);
                    for (int i = 0; i < limit; i++)
                        inspected.Add(ReflectionUtil.Inspect(args[i], _config.EventArgDepth, _config.EventArgMaxMembers, _config.EventArgMaxCollectionItems));

                    ProbeLog.AuditEvent(new Dictionary<string, object>
                    {
                        { "utc", DateTime.UtcNow }, { "kind", "delegate-event" }, { "event", eventName }, { "scope", scope }, { "args", inspected }
                    });
                }
            }
            catch (Exception ex) { ProbeLog.Warn("Event sink failed for " + eventName + ": " + ex.Message); }
        }

        private static void Capability(string kind, string name, bool ok, string detail)
        {
            ProbeLog.Capability(new Dictionary<string, object>
            {
                { "utc", DateTime.UtcNow }, { "capability", kind }, { "name", name }, { "ok", ok }, { "detail", detail }
            });
        }
    }
}
