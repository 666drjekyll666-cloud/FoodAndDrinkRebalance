using BepInEx;
using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace GKFoodRebalance
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class GKFoodRebalancePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.graveyardkeeper.gkfoodrebalance";
        public const string PluginName = "Food & Drink Rebalance";
        public const string PluginVersion = "1.2.1";

        private const string LegacyWellFedShortBuffId = "gkfr_wellfed_short";
        private const string LegacyWellFedLongBuffId = "gkfr_wellfed_long";
        private const string WellFedBuffId = "gkfr_wellfed";
        private const string WellFedLongTriggerBuffId = "gkfr_wellfed_long_trigger";
        private const string WellFedLongTriggerResource = "gkfr_wellfed_long_apply";
        private const string WellFedResource = "gkfr_wellfed";
        private const string SpeedFoodBuffId = "gkfr_speed_food";
        private const string InebriatedBuffId = "gkfr_inebriated";
        private const string SoberingFoodTriggerBuffId = "gkfr_sobering_food_trigger";
        private const string SoberingFoodTriggerResource = "gkfr_sobering_food_trigger";
        private const float WellFedCraftSpeedMultiplier = 2.00f;
        private const float InebriatedSpeedBuff = -0.33f;
        // Verified BuffsLogics.AddBuff conversion for a source length of one nominal minute.
        private const float InebriatedDoseEndTimeDelta = 60f / 450f;

        private static ManualLogSource Log;
        private static readonly BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static string _lastInjectedLanguage;
        private static MethodInfo _findBuffByIdMethod;
        private static MethodInfo _removeBuffMethod;
        private static Type _mainGameType;
        private const int MaxCraftDiagnosticEntries = 64;
        private static readonly HashSet<string> CraftDiagnosticSeen = new HashSet<string>(StringComparer.Ordinal);

        private static readonly string[] ExcludedCraftWgoFragments =
        {
            "zombie", "refugee", "bee", "tree", "berry", "bush", "pump", "compost",
            "peat", "slime", "candelabrum", "incense", "garden", "planting"
        };

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo("Food & Drink Rebalance 1.2.1 DIAGNOSTIC loading.");
            Logger.LogInfo("WELLFED_DIAGNOSTIC_READY mode=research-only max_unique_entries=" + MaxCraftDiagnosticEntries);

            try
            {
                InstallLanguagePatch();
            }
            catch (Exception ex)
            {
                Logger.LogError("Language hook failed: " + ex);
            }

            TryInjectLocalization();
            StartCoroutine(InitializeWhenReady());
        }

        private IEnumerator InitializeWhenReady()
        {
            object balance = null;
            while (balance == null)
            {
                Type balanceType = FindType("GameBalance");
                if (balanceType != null) balance = GetStaticMember(balanceType, "me");
                if (balance == null) yield return null;
            }

            IList items = GetMember(balance, "items_data") as IList;
            IList crafts = GetMember(balance, "craft_data") as IList;
            IList buffs = GetMember(balance, "buffs_data") as IList;

            if (items == null || crafts == null || buffs == null)
            {
                Logger.LogError("GameBalance collections not found.");
                yield break;
            }

            try
            {
                ApplyNumericBalance(items, crafts);
                Logger.LogInfo("Numeric food rebalance applied.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Numeric rebalance failed: " + ex);
            }

            bool customBuffsReady = false;
            try
            {
                EnsureCustomBuffs(buffs);
                customBuffsReady = true;
                Logger.LogInfo("Unified visible food buffs created.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Custom buff setup failed; numeric rebalance remains active: " + ex);
            }

            if (customBuffsReady)
            {
                try
                {
                    // Keep the accepted 1.0 egg bindings as the fail-closed baseline.
                    // The validated alcohol/egg module below atomically moves Well Fed from boiled egg to omelette-only.
                    RebindWellFedItem(items, "snack:boiled_egg", false);
                    RebindWellFedItem(items, "meal:omlette", true);
                    AttachBuff(items, "dessert:jelly_green", SpeedFoodBuffId);
                    Logger.LogInfo("Food buff baseline applied: 1.0 Well Fed bindings + generic Speed presentation.");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Food buff binding failed: " + ex);
                }
            }

            try
            {
                if (TryApplyAlcoholModule(items, buffs))
                    Logger.LogInfo("Alcohol/egg module applied: Fried Egg 15 + one-dose Sobering, Boiled Egg plain 20, Omelette-only Well Fed, Wine 30/40/50, Mead 50/60/70, both alcohol families Inebriated.");
                else
                    Logger.LogWarning("Alcohol/egg module disabled because its validated Graveyard Keeper 1.407 runtime contract did not match. Existing 1.0 food/egg balance remains active.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Alcohol/egg module failed closed; existing 1.0 food/egg balance remains active: " + ex);
            }

            try
            {
                InstallCraftSpeedPatch();
                Logger.LogInfo("Well Fed craft-speed hook installed (x2.00; vanilla energy untouched).");
            }
            catch (Exception ex)
            {
                Logger.LogError("Well Fed craft-speed hook failed: " + ex);
            }

            TryInjectLocalization();
            Logger.LogInfo("FOOD & DRINK REBALANCE READY");
        }

        private static void ApplyNumericBalance(IList items, IList crafts)
        {
            SetFood(items, "meal:beet_slice", 20f, 10f);
            SetFood(items, "snack:grated_carrot", 30f, null);
            SetFood(items, "snack:fried_egg", 25f, null);
            SetFood(items, "snack:boiled_egg", 20f, null);
            SetFood(items, "meal:omlette", 30f, null);
            SetFood(items, "dessert:jelly_green", 30f, null);
            SetFood(items, "dessert:jelly_red", 45f, null);
            SetFood(items, "snack:sandwich", 40f, null);
            SetFood(items, "dessert:croissant", 35f, null);
            SetFood(items, "dessert:pie_1", 60f, null);
            SetFood(items, "meal:creamy_vegetable_soup:1", 20f, null);
            SetFood(items, "meal:creamy_vegetable_soup:2", 25f, null);
            SetFood(items, "meal:creamy_vegetable_soup:3", 30f, null);
            SetFood(items, "snack:toasts:1", 24f, null);
            SetFood(items, "snack:toasts:2", 32f, null);
            SetFood(items, "snack:toasts:3", 40f, null);
            SetFood(items, "meal:burger:1", 50f, null);
            SetFood(items, "meal:burger:2", 60f, null);
            SetFood(items, "meal:burger:3", 70f, null);

            SetSingleInputCraftCountByOutput(crafts, "snack:grated_carrot", 1, 2);
            SetCraftInputCount(crafts, "grated_beetroot", "beet_crop", 2);
            SetCraftInputCount(crafts, "camp_kitchen_grated_beetroot", "beet_crop", 2);
            SetCraftInputCount(crafts, "cutlet_carrot", "carrot_crop", 3);
            SetCraftOutputCount(crafts, "beet_slice", "meal:beet_slice", 2);
            SetCraftOutputCount(crafts, "honey_cake", "dessert:honey_cake", 2);
        }

        private static void SetFood(IList items, string id, float energy, float? hp)
        {
            object item = FindById(items, id);
            if (item == null)
            {
                Log.LogWarning("Item not found: " + id);
                return;
            }

            object paramsOnUse = GetMember(item, "params_on_use");
            if (paramsOnUse != null)
            {
                SetGameResNamedValue(paramsOnUse, "energy", energy);
                if (hp.HasValue) SetMember(paramsOnUse, "_hp", hp.Value);
            }

            IList expressions = GetMember(item, "on_use_expressions") as IList;
            if (expressions != null) SetEnergyExpression(expressions, energy);

            Log.LogInfo("Balanced " + id + " => E " + F(energy) + (hp.HasValue ? ", HP " + F(hp.Value) : ""));
        }

        private static void SetEnergyExpression(IList expressions, float energy)
        {
            string text = "AddPpar(\"energy\", " + F(energy) + "*Ppar(\"food_multiplier\"))";
            for (int i = 0; i < expressions.Count; i++)
            {
                object expression = expressions[i];
                string current = GetMember(expression, "_expression") as string;
                if (!string.IsNullOrEmpty(current) && current.Contains("AddPpar(\"energy\""))
                {
                    SetSmartExpression(expression, text);
                    return;
                }
            }

            if (expressions.Count > 0)
            {
                object clone = ShallowClone(expressions[0]);
                SetSmartExpression(clone, text);
                expressions.Add(clone);
            }
        }

        private sealed class AlcoholTarget
        {
            public string Id;
            public float ExpectedEnergy;
            public float ExpectedHp;
            public float NewEnergy;
            public float NewHp;
            public string ExpectedPerkExpression;
            public string NewPerkExpression;
            public object Item;
        }

        private static bool TryApplyAlcoholModule(IList items, IList buffs)
        {
            AlcoholTarget[] targets =
            {
                new AlcoholTarget { Id = "bottle_red_vine:1", ExpectedEnergy = 60f, ExpectedHp = 30f, NewEnergy = 30f, NewHp = 30f, ExpectedPerkExpression = "AddPpar(\"energy\", 25*Ppar(\"p_wine_master\"))", NewPerkExpression = "AddPpar(\"energy\", 6*Ppar(\"p_wine_master\"))" },
                new AlcoholTarget { Id = "bottle_red_vine:2", ExpectedEnergy = 72f, ExpectedHp = 30f, NewEnergy = 40f, NewHp = 30f, ExpectedPerkExpression = "AddPpar(\"energy\", 30*Ppar(\"p_wine_master\"))", NewPerkExpression = "AddPpar(\"energy\", 8*Ppar(\"p_wine_master\"))" },
                new AlcoholTarget { Id = "bottle_red_vine:3", ExpectedEnergy = 84f, ExpectedHp = 30f, NewEnergy = 50f, NewHp = 30f, ExpectedPerkExpression = "AddPpar(\"energy\", 30*Ppar(\"p_wine_master\"))", NewPerkExpression = "AddPpar(\"energy\", 10*Ppar(\"p_wine_master\"))" },
                new AlcoholTarget { Id = "cup_mead:1", ExpectedEnergy = 12f, ExpectedHp = 5f, NewEnergy = 50f, NewHp = 0f, ExpectedPerkExpression = "AddPpar(\"energy\", 5*Ppar(\"p_wine_master\"))", NewPerkExpression = "AddPpar(\"energy\", 10*Ppar(\"p_wine_master\"))" },
                new AlcoholTarget { Id = "cup_mead:2", ExpectedEnergy = 18f, ExpectedHp = 5f, NewEnergy = 60f, NewHp = 0f, ExpectedPerkExpression = "AddPpar(\"energy\", 8*Ppar(\"p_wine_master\"))", NewPerkExpression = "AddPpar(\"energy\", 12*Ppar(\"p_wine_master\"))" },
                new AlcoholTarget { Id = "cup_mead:3", ExpectedEnergy = 24f, ExpectedHp = 5f, NewEnergy = 70f, NewHp = 0f, ExpectedPerkExpression = "AddPpar(\"energy\", 10*Ppar(\"p_wine_master\"))", NewPerkExpression = "AddPpar(\"energy\", 14*Ppar(\"p_wine_master\"))" }
            };

            object speedTemplate = FindById(buffs, "buff_pot_speed");
            if (!ValidateSpeedBuffTemplate(speedTemplate))
                return AlcoholContractFailure("buff_pot_speed did not match the verified additive speed_buff=1.5 contract.");
            if (GetMember(speedTemplate, "length") == null || GetMember(speedTemplate, "res") == null)
                return AlcoholContractFailure("buff_pot_speed length/res fields were not available.");

            MethodInfo addBuff;
            MethodInfo findBuffById;
            MethodInfo removeBuff;
            Type mainGameType;
            if (!TryResolveBuffMethods(out addBuff, out findBuffById, out removeBuff, out mainGameType))
                return AlcoholContractFailure("BuffsLogics AddBuff/FindBuffByID/RemoveBuff plus PlayerBuff.end_time/MainGame.game_time contract missing.");

            for (int i = 0; i < targets.Length; i++)
            {
                AlcoholTarget target = targets[i];
                target.Item = FindById(items, target.Id);
                if (target.Item == null)
                    return AlcoholContractFailure("required alcohol item missing: " + target.Id);

                float energy;
                float hp;
                if (!TryReadNamedResource(GetMember(target.Item, "params_on_use"), "energy", out energy) ||
                    !FloatEquals(energy, target.ExpectedEnergy))
                    return AlcoholContractFailure(target.Id + " expected vanilla Energy " + F(target.ExpectedEnergy) + ", got " + F(energy) + ".");

                if (!TryReadScalarFloat(GetMember(target.Item, "params_on_use"), "_hp", out hp) ||
                    !FloatEquals(hp, target.ExpectedHp))
                    return AlcoholContractFailure(target.Id + " expected vanilla HP " + F(target.ExpectedHp) + ", got " + F(hp) + ".");

                IList expressions = GetMember(target.Item, "on_use_expressions") as IList;
                if (expressions == null || FindExpression(expressions, target.ExpectedPerkExpression) == null)
                    return AlcoholContractFailure(target.Id + " Wine Master expression did not match verified runtime data.");
            }

            object friedEgg = FindById(items, "snack:fried_egg");
            object boiledEgg = FindById(items, "snack:boiled_egg");
            if (friedEgg == null || boiledEgg == null)
                return AlcoholContractFailure("required egg item missing.");

            float friedEnergy;
            if (!TryReadNamedResource(GetMember(friedEgg, "params_on_use"), "energy", out friedEnergy) || !FloatEquals(friedEnergy, 25f))
                return AlcoholContractFailure("Fried Egg did not match accepted 1.0 baseline Energy 25 before egg-role mutation.");
            IList friedExpressions = GetMember(friedEgg, "on_use_expressions") as IList;
            IList boiledExpressions = GetMember(boiledEgg, "on_use_expressions") as IList;
            if (friedExpressions == null || FindExpression(friedExpressions, "AddPpar(\"energy\", 25*Ppar(\"food_multiplier\"))") == null)
                return AlcoholContractFailure("Fried Egg accepted 1.0 energy expression was not present.");
            if (boiledExpressions == null || !ContainsBuffExpression(boiledExpressions, WellFedBuffId))
                return AlcoholContractFailure("Boiled Egg accepted 1.0 Well Fed binding was not present before egg-role mutation.");

            if (FindById(buffs, InebriatedBuffId) != null || FindById(buffs, SoberingFoodTriggerBuffId) != null)
                return AlcoholContractFailure("custom alcohol/sobering buff IDs already exist before GK Food Rebalance applies them.");

            object inebriated = CreateInebriatedBuff(speedTemplate);
            object soberingTrigger = CreateSoberingFoodTriggerBuff(speedTemplate);
            if (inebriated == null || soberingTrigger == null)
                return AlcoholContractFailure("failed to build custom alcohol/sobering buff definitions from verified template.");

            _findBuffByIdMethod = findBuffById;
            _removeBuffMethod = removeBuff;
            _mainGameType = mainGameType;
            try
            {
                InstallSoberingFoodPatch(addBuff);
            }
            catch
            {
                _findBuffByIdMethod = null;
                _removeBuffMethod = null;
                _mainGameType = null;
                throw;
            }

            try
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    AlcoholTarget target = targets[i];
                    object paramsOnUse = GetMember(target.Item, "params_on_use");
                    SetGameResNamedValue(paramsOnUse, "energy", target.NewEnergy);
                    SetMember(paramsOnUse, "_hp", target.NewHp);

                    IList expressions = GetMember(target.Item, "on_use_expressions") as IList;
                    object expression = FindExpression(expressions, target.ExpectedPerkExpression);
                    SetSmartExpression(expression, target.NewPerkExpression);
                }

                SetGameResNamedValue(GetMember(friedEgg, "params_on_use"), "energy", 15f);
                SetEnergyExpression(friedExpressions, 15f);

                RemoveBuffExpression(boiledExpressions, WellFedBuffId);
                RemoveBuffExpression(boiledExpressions, LegacyWellFedShortBuffId);
                RemoveBuffExpression(boiledExpressions, LegacyWellFedLongBuffId);
                RemoveBuffExpression(boiledExpressions, WellFedLongTriggerBuffId);

                buffs.Add(inebriated);
                buffs.Add(soberingTrigger);

                for (int i = 0; i < targets.Length; i++)
                    AttachBuff(items, targets[i].Id, InebriatedBuffId);
                AttachBuff(items, "snack:fried_egg", SoberingFoodTriggerBuffId);

                if (!VerifyAlcoholModule(targets, buffs, friedEgg, boiledEgg))
                    throw new InvalidOperationException("Alcohol/egg post-apply verification failed.");

                return true;
            }
            catch (Exception ex)
            {
                RollbackAlcoholModule(targets, buffs, items, friedEgg);
                if (Log != null) Log.LogError("[AlcoholEgg] Apply/verify failed; new alcohol/egg mutations rolled back to accepted 1.0 behavior: " + ex.Message);
                return false;
            }
        }

        private static void RollbackAlcoholModule(AlcoholTarget[] targets, IList buffs, IList items, object friedEgg)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                AlcoholTarget target = targets[i];
                if (target.Item == null) continue;
                object paramsOnUse = GetMember(target.Item, "params_on_use");
                SetGameResNamedValue(paramsOnUse, "energy", target.ExpectedEnergy);
                SetMember(paramsOnUse, "_hp", target.ExpectedHp);

                IList expressions = GetMember(target.Item, "on_use_expressions") as IList;
                object changed = FindExpression(expressions, target.NewPerkExpression);
                if (changed != null) SetSmartExpression(changed, target.ExpectedPerkExpression);
                RemoveBuffExpression(expressions, InebriatedBuffId);
            }

            if (friedEgg != null)
            {
                SetGameResNamedValue(GetMember(friedEgg, "params_on_use"), "energy", 25f);
                IList friedExpressions = GetMember(friedEgg, "on_use_expressions") as IList;
                SetEnergyExpression(friedExpressions, 25f);
                RemoveBuffExpression(friedExpressions, SoberingFoodTriggerBuffId);
            }

            RebindWellFedItem(items, "snack:boiled_egg", false);

            RemoveById(buffs, InebriatedBuffId);
            RemoveById(buffs, SoberingFoodTriggerBuffId);
            _findBuffByIdMethod = null;
            _removeBuffMethod = null;
            _mainGameType = null;
        }

        private static void RemoveBuffExpression(IList expressions, string buffId)
        {
            if (expressions == null) return;
            string expected = "AddBuff(\"" + buffId + "\")";
            for (int i = expressions.Count - 1; i >= 0; i--)
            {
                string current = GetMember(expressions[i], "_expression") as string;
                if (NormalizeExpression(current) == expected) expressions.RemoveAt(i);
            }
        }

        private static void RemoveById(IList list, string id)
        {
            if (list == null) return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (string.Equals(GetMember(list[i], "id") as string, id, StringComparison.Ordinal))
                    list.RemoveAt(i);
            }
        }

        private static bool ValidateSpeedBuffTemplate(object speedTemplate)
        {
            if (speedTemplate == null) return false;
            if (!string.Equals(Convert.ToString(GetMember(speedTemplate, "overlay_type"), CultureInfo.InvariantCulture), "Add", StringComparison.OrdinalIgnoreCase))
                return false;

            float speedBuff;
            return TryReadNamedResource(GetMember(speedTemplate, "res"), "speed_buff", out speedBuff) &&
                   FloatEquals(speedBuff, 1.5f);
        }

        private static bool TryResolveBuffMethods(out MethodInfo addBuff, out MethodInfo findBuffById, out MethodInfo removeBuff, out Type mainGameType)
        {
            addBuff = null;
            findBuffById = null;
            removeBuff = null;
            mainGameType = null;

            Type buffsType = FindType("BuffsLogics");
            Type playerBuffType = FindType("PlayerBuff");
            mainGameType = FindType("MainGame");
            if (buffsType == null || playerBuffType == null || mainGameType == null) return false;

            MethodInfo[] methods = buffsType.GetMethods(AnyStatic);
            addBuff = methods.FirstOrDefault(m =>
            {
                if (m.Name != "AddBuff") return false;
                ParameterInfo[] p = m.GetParameters();
                return p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(float?);
            });
            findBuffById = methods.FirstOrDefault(m =>
            {
                if (m.Name != "FindBuffByID") return false;
                ParameterInfo[] p = m.GetParameters();
                return p.Length == 1 && p[0].ParameterType == typeof(string);
            });
            removeBuff = methods.FirstOrDefault(m =>
            {
                if (m.Name != "RemoveBuff") return false;
                ParameterInfo[] p = m.GetParameters();
                return p.Length == 1 && p[0].ParameterType == typeof(string);
            });

            FieldInfo endTimeField = playerBuffType.GetField("end_time", AnyInstance);
            PropertyInfo endTimeProperty = playerBuffType.GetProperty("end_time", AnyInstance);
            bool endTimeWritable = endTimeField != null || (endTimeProperty != null && endTimeProperty.CanRead && endTimeProperty.CanWrite);

            FieldInfo gameTimeField = mainGameType.GetField("game_time", AnyStatic);
            PropertyInfo gameTimeProperty = mainGameType.GetProperty("game_time", AnyStatic);
            bool gameTimeReadable = gameTimeField != null || (gameTimeProperty != null && gameTimeProperty.CanRead);

            return addBuff != null && findBuffById != null && removeBuff != null && endTimeWritable && gameTimeReadable;
        }

        private static object CreateInebriatedBuff(object speedTemplate)
        {
            object clone = ShallowClone(speedTemplate);
            SetMember(clone, "id", InebriatedBuffId);
            SetMember(clone, "is_hidden", false);
            SetMember(clone, "do_not_show_timer", false);
            SetMember(clone, "custom_icon", "b_drunk");
            SetMember(clone, "craft_q", 0f);

            object length = ShallowClone(GetMember(speedTemplate, "length"));
            SetSmartExpression(length, "1");
            SetMember(clone, "length", length);

            object res = ShallowClone(GetMember(speedTemplate, "res"));
            ReplaceList(res, "_res_type", new object[] { "speed_buff" });
            ReplaceList(res, "_res_v", new object[] { InebriatedSpeedBuff });
            ClearScalarGameRes(res);
            SetMember(clone, "res", res);
            return clone;
        }

        private static object CreateSoberingFoodTriggerBuff(object speedTemplate)
        {
            object clone = ShallowClone(speedTemplate);
            SetMember(clone, "id", SoberingFoodTriggerBuffId);
            SetMember(clone, "is_hidden", false);
            SetMember(clone, "do_not_show_timer", true);
            SetMember(clone, "custom_icon", "b_hangover");
            SetMember(clone, "craft_q", 0f);

            object length = ShallowClone(GetMember(speedTemplate, "length"));
            SetSmartExpression(length, "0.001");
            SetMember(clone, "length", length);

            object res = ShallowClone(GetMember(speedTemplate, "res"));
            ReplaceList(res, "_res_type", new object[] { SoberingFoodTriggerResource });
            ReplaceList(res, "_res_v", new object[] { 1f });
            ClearScalarGameRes(res);
            SetMember(clone, "res", res);
            return clone;
        }

        private static void ClearScalarGameRes(object res)
        {
            SetMember(res, "_hp", 0f);
            SetMember(res, "_money", 0f);
            SetMember(res, "_progress", 0f);
            SetMember(res, "_durability", 0f);
        }

        private static object FindExpression(IList expressions, string expected)
        {
            if (expressions == null || string.IsNullOrEmpty(expected)) return null;
            string normalizedExpected = NormalizeExpression(expected);
            for (int i = 0; i < expressions.Count; i++)
            {
                string current = GetMember(expressions[i], "_expression") as string;
                if (NormalizeExpression(current) == normalizedExpected) return expressions[i];
            }
            return null;
        }

        private static string NormalizeExpression(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
        }

        private static bool TryReadNamedResource(object gameRes, string name, out float value)
        {
            return TryReadGameRes(gameRes, name, out value);
        }

        private static bool TryReadScalarFloat(object obj, string memberName, out float value)
        {
            value = 0f;
            return TryToFloat(GetMember(obj, memberName), out value);
        }

        private static bool FloatEquals(float a, float b)
        {
            return Math.Abs(a - b) <= 0.001f;
        }

        private static bool AlcoholContractFailure(string reason)
        {
            if (Log != null) Log.LogWarning("[AlcoholEgg] Validation failed: " + reason);
            return false;
        }

        private static void InstallSoberingFoodPatch(MethodInfo addBuff)
        {
            MethodInfo postfix = typeof(GKFoodRebalancePlugin).GetMethod(nameof(BuffsAddBuffPostfix), AnyStatic);
            if (postfix == null) throw new MissingMethodException("Sobering-food postfix missing.");
            PatchMethod(PluginGuid + ".soberingfood", addBuff, null, postfix);
        }

        private static void BuffsAddBuffPostfix(object[] __args)
        {
            if (__args == null || __args.Length == 0 || !string.Equals(__args[0] as string, SoberingFoodTriggerBuffId, StringComparison.Ordinal)) return;

            try
            {
                if (_findBuffByIdMethod == null || _removeBuffMethod == null || _mainGameType == null)
                {
                    if (Log != null) Log.LogWarning("Sobering-food relief skipped: verified buff-timer contract unavailable.");
                    return;
                }

                _removeBuffMethod.Invoke(null, new object[] { SoberingFoodTriggerBuffId });

                object active = _findBuffByIdMethod.Invoke(null, new object[] { InebriatedBuffId });
                if (active == null)
                {
                    if (Log != null) Log.LogInfo("Fried Egg used while sober; no Inebriated duration to reduce.");
                    return;
                }

                float endTime;
                float gameTime;
                if (!TryToFloat(GetMember(active, "end_time"), out endTime) ||
                    !TryToFloat(GetStaticMember(_mainGameType, "game_time"), out gameTime))
                    throw new InvalidOperationException("Could not read verified PlayerBuff.end_time/MainGame.game_time values.");

                float remaining = endTime - gameTime;
                if (remaining <= InebriatedDoseEndTimeDelta + 0.0001f)
                {
                    _removeBuffMethod.Invoke(null, new object[] { InebriatedBuffId });
                    if (Log != null) Log.LogInfo("Fried Egg removed the remaining Inebriated duration (one dose or less).");
                    return;
                }

                SetMember(active, "end_time", endTime - InebriatedDoseEndTimeDelta);
                if (Log != null) Log.LogInfo("Fried Egg reduced Inebriated by exactly one alcohol dose.");
            }
            catch (Exception ex)
            {
                if (Log != null) Log.LogWarning("Sobering-food one-dose relief hook error: " + ex.Message);
            }
        }

        private static bool VerifyAlcoholModule(AlcoholTarget[] targets, IList buffs, object friedEgg, object boiledEgg)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                float energy;
                float hp;
                object paramsOnUse = GetMember(targets[i].Item, "params_on_use");
                if (!TryReadNamedResource(paramsOnUse, "energy", out energy) || !FloatEquals(energy, targets[i].NewEnergy)) return false;
                if (!TryReadScalarFloat(paramsOnUse, "_hp", out hp) || !FloatEquals(hp, targets[i].NewHp)) return false;

                IList expressions = GetMember(targets[i].Item, "on_use_expressions") as IList;
                if (!ContainsBuffExpression(expressions, InebriatedBuffId)) return false;
                if (FindExpression(expressions, targets[i].NewPerkExpression) == null) return false;
            }

            float friedEnergy;
            IList friedExpressions = GetMember(friedEgg, "on_use_expressions") as IList;
            IList boiledExpressions = GetMember(boiledEgg, "on_use_expressions") as IList;
            if (!TryReadNamedResource(GetMember(friedEgg, "params_on_use"), "energy", out friedEnergy) || !FloatEquals(friedEnergy, 15f)) return false;
            if (!ContainsBuffExpression(friedExpressions, SoberingFoodTriggerBuffId)) return false;
            if (ContainsBuffExpression(boiledExpressions, WellFedBuffId)) return false;

            object inebriated = FindById(buffs, InebriatedBuffId);
            object sobering = FindById(buffs, SoberingFoodTriggerBuffId);
            float slow;
            return inebriated != null && sobering != null &&
                   TryReadNamedResource(GetMember(inebriated, "res"), "speed_buff", out slow) &&
                   FloatEquals(slow, InebriatedSpeedBuff);
        }

        private static bool ContainsBuffExpression(IList expressions, string buffId)
        {
            if (expressions == null) return false;
            for (int i = 0; i < expressions.Count; i++)
            {
                string existing = GetMember(expressions[i], "_expression") as string;
                if (!string.IsNullOrEmpty(existing) && NormalizeExpression(existing).Contains("AddBuff(\"" + buffId + "\")"))
                    return true;
            }
            return false;
        }

        private static void EnsureCustomBuffs(IList buffs)
        {
            if (FindById(buffs, WellFedBuffId) == null)
                buffs.Add(CreateUnifiedWellFedBuff(buffs));

            if (FindById(buffs, WellFedLongTriggerBuffId) == null)
                buffs.Add(CreateLongTriggerBuff(buffs));

            if (FindById(buffs, SpeedFoodBuffId) == null)
                buffs.Add(CreateShortSpeedBuff(buffs));
        }

        private static object CreateUnifiedWellFedBuff(IList buffs)
        {
            object template = FindById(buffs, "buff_beer") ?? FindById(buffs, "buff_hardwork");
            if (template == null) throw new InvalidOperationException("Food buff template missing.");

            object clone = ShallowClone(template);
            SetMember(clone, "id", WellFedBuffId);
            SetMember(clone, "is_hidden", false);
            SetMember(clone, "do_not_show_timer", false);
            SetMember(clone, "custom_icon", "b_hammer");
            SetMember(clone, "craft_q", 0f);

            object length = ShallowClone(GetMember(template, "length"));
            SetSmartExpression(length,
                "(0.75+1.25*Ppar(\"" + WellFedLongTriggerResource + "\"))*(1+Ppar(\"buff_longtimer\")*0.3)");
            SetMember(clone, "length", length);

            object res = ShallowClone(GetMember(template, "res"));
            ReplaceList(res, "_res_type", new object[] { WellFedResource });
            ReplaceList(res, "_res_v", new object[] { 1f });
            SetMember(res, "_hp", 0f);
            SetMember(res, "_money", 0f);
            SetMember(res, "_progress", 0f);
            SetMember(res, "_durability", 0f);
            SetMember(clone, "res", res);
            return clone;
        }

        private static object CreateLongTriggerBuff(IList buffs)
        {
            object template = FindById(buffs, "buff_beer") ?? FindById(buffs, "buff_hardwork");
            if (template == null) throw new InvalidOperationException("Food buff template missing.");

            object clone = ShallowClone(template);
            SetMember(clone, "id", WellFedLongTriggerBuffId);
            SetMember(clone, "is_hidden", true);
            SetMember(clone, "do_not_show_timer", true);
            SetMember(clone, "craft_q", 0f);

            object length = ShallowClone(GetMember(template, "length"));
            SetSmartExpression(length, "0.05");
            SetMember(clone, "length", length);

            object res = ShallowClone(GetMember(template, "res"));
            ReplaceList(res, "_res_type", new object[] { WellFedLongTriggerResource });
            ReplaceList(res, "_res_v", new object[] { 1f });
            SetMember(res, "_hp", 0f);
            SetMember(res, "_money", 0f);
            SetMember(res, "_progress", 0f);
            SetMember(res, "_durability", 0f);
            SetMember(clone, "res", res);
            return clone;
        }

        private static object CreateShortSpeedBuff(IList buffs)
        {
            object template = FindById(buffs, "buff_pot_speed");
            if (template == null) throw new InvalidOperationException("buff_pot_speed missing.");

            object clone = ShallowClone(template);
            SetMember(clone, "id", SpeedFoodBuffId);
            SetMember(clone, "is_hidden", false);
            SetMember(clone, "do_not_show_timer", false);
            SetMember(clone, "custom_icon", "b_run");

            object length = ShallowClone(GetMember(template, "length"));
            SetSmartExpression(length, "1*(1+Ppar(\"buff_longtimer\")*0.3)");
            SetMember(clone, "length", length);

            object res = ShallowClone(GetMember(template, "res"));
            CloneListMember(res, "_res_type");
            CloneListMember(res, "_res_v");
            SetMember(clone, "res", res);
            return clone;
        }

        private static void RebindWellFedItem(IList items, string itemId, bool longDuration)
        {
            object item = FindById(items, itemId);
            if (item == null)
            {
                Log.LogWarning("Cannot bind Well Fed; item missing: " + itemId);
                return;
            }

            IList expressions = GetMember(item, "on_use_expressions") as IList;
            if (expressions == null || expressions.Count == 0)
            {
                Log.LogWarning("Cannot bind Well Fed; no expressions on " + itemId);
                return;
            }

            for (int i = expressions.Count - 1; i >= 0; i--)
            {
                string existing = GetMember(expressions[i], "_expression") as string;
                if (string.IsNullOrEmpty(existing)) continue;
                if (existing.Contains(LegacyWellFedShortBuffId) || existing.Contains(LegacyWellFedLongBuffId) ||
                    existing.Contains(WellFedBuffId) || existing.Contains(WellFedLongTriggerBuffId))
                    expressions.RemoveAt(i);
            }

            if (expressions.Count == 0)
                throw new InvalidOperationException("No expression template remains on " + itemId + ".");

            if (longDuration)
                AddExpression(expressions, "AddBuff(\"" + WellFedLongTriggerBuffId + "\")");
            AddExpression(expressions, "AddBuff(\"" + WellFedBuffId + "\")");

            Log.LogInfo("Bound " + itemId + " => unified Well Fed " + (longDuration ? "long" : "short") + " duration.");
        }

        private static void AttachBuff(IList items, string itemId, string buffId)
        {
            object item = FindById(items, itemId);
            if (item == null)
            {
                Log.LogWarning("Cannot attach " + buffId + "; item missing: " + itemId);
                return;
            }

            IList expressions = GetMember(item, "on_use_expressions") as IList;
            if (expressions == null || expressions.Count == 0)
            {
                Log.LogWarning("Cannot attach " + buffId + "; no expressions on " + itemId);
                return;
            }

            for (int i = 0; i < expressions.Count; i++)
            {
                string existing = GetMember(expressions[i], "_expression") as string;
                if (!string.IsNullOrEmpty(existing) && existing.Contains(buffId)) return;
            }

            AddExpression(expressions, "AddBuff(\"" + buffId + "\")");
        }

        private static void AddExpression(IList expressions, string text)
        {
            object clone = ShallowClone(expressions[0]);
            SetSmartExpression(clone, text);
            expressions.Add(clone);
        }

        private static void SetCraftOutputCount(IList crafts, string craftId, string outputItemId, int count)
        {
            object craft = FindById(crafts, craftId);
            if (craft == null)
            {
                Log.LogWarning("Craft not found: " + craftId);
                return;
            }

            IList outputs = GetMember(craft, "output") as IList;
            if (outputs == null)
            {
                Log.LogWarning("Craft has no output list: " + craftId);
                return;
            }

            for (int i = 0; i < outputs.Count; i++)
            {
                object output = outputs[i];
                if (string.Equals(GetMember(output, "id") as string, outputItemId, StringComparison.Ordinal))
                {
                    SetMember(output, "value", count);
                    Log.LogInfo("Balanced craft " + craftId + " => " + outputItemId + " x" + count);
                    return;
                }
            }

            Log.LogWarning("Output " + outputItemId + " not found in craft " + craftId + ".");
        }

        private static void SetCraftInputCount(IList crafts, string craftId, string inputItemId, int count)
        {
            object craft = FindById(crafts, craftId);
            if (craft == null)
            {
                Log.LogWarning("Craft not found: " + craftId);
                return;
            }

            IList needs = GetMember(craft, "needs") as IList;
            if (needs == null)
            {
                Log.LogWarning("Craft has no needs list: " + craftId);
                return;
            }

            for (int i = 0; i < needs.Count; i++)
            {
                object need = needs[i];
                if (string.Equals(GetMember(need, "id") as string, inputItemId, StringComparison.Ordinal))
                {
                    SetMember(need, "value", count);
                    Log.LogInfo("Balanced craft " + craftId + " input => " + inputItemId + " x" + count);
                    return;
                }
            }

            Log.LogWarning("Input " + inputItemId + " not found in craft " + craftId + ".");
        }

        private static void SetCraftOutputCountByOutput(IList crafts, string outputItemId, int expectedOldCount, int newCount)
        {
            int changed = 0;
            for (int i = 0; i < crafts.Count; i++)
            {
                object craft = crafts[i];
                IList outputs = GetMember(craft, "output") as IList;
                if (outputs == null) continue;

                for (int o = 0; o < outputs.Count; o++)
                {
                    object output = outputs[o];
                    if (!string.Equals(GetMember(output, "id") as string, outputItemId, StringComparison.Ordinal)) continue;

                    object value = GetMember(output, "value");
                    int current;
                    try { current = Convert.ToInt32(value, CultureInfo.InvariantCulture); }
                    catch { continue; }
                    if (current != expectedOldCount) continue;

                    SetMember(output, "value", newCount);
                    changed++;
                    Log.LogInfo("Balanced craft " + ((GetMember(craft, "id") as string) ?? "<unknown>") + " => " + outputItemId + " x" + newCount);
                }
            }

            if (changed == 0)
                Log.LogWarning("No craft output matched " + outputItemId + " x" + expectedOldCount + " for update to x" + newCount + ".");
        }

        private static void SetSingleInputCraftCountByOutput(IList crafts, string outputItemId, int expectedOldCount, int newCount)
        {
            int changed = 0;
            for (int i = 0; i < crafts.Count; i++)
            {
                object craft = crafts[i];
                if (!CraftOutputsItem(craft, outputItemId)) continue;

                IList inputs = GetMember(craft, "needs") as IList;
                if (inputs == null || inputs.Count != 1) continue;

                object input = inputs[0];
                object value = GetMember(input, "value");
                int current;
                try { current = Convert.ToInt32(value, CultureInfo.InvariantCulture); }
                catch { continue; }
                if (current != expectedOldCount) continue;

                SetMember(input, "value", newCount);
                changed++;
                Log.LogInfo("Balanced craft " + ((GetMember(craft, "id") as string) ?? "<unknown>") + " input => " + expectedOldCount + " -> " + newCount + " for " + outputItemId);
            }

            if (changed == 0)
                Log.LogWarning("No single-input craft matched " + outputItemId + " with input x" + expectedOldCount + " for update to x" + newCount + ".");
        }

        private static bool CraftOutputsItem(object craft, string outputItemId)
        {
            IList outputs = GetMember(craft, "output") as IList;
            if (outputs == null) return false;
            for (int i = 0; i < outputs.Count; i++)
                if (string.Equals(GetMember(outputs[i], "id") as string, outputItemId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static void InstallLanguagePatch()
        {
            Type gjlType = FindType("GJL");
            if (gjlType == null) throw new InvalidOperationException("GJL type missing.");

            MethodInfo target = gjlType.GetMethods(AnyStatic)
                .FirstOrDefault(m => m.Name == "LoadLanguageResource" &&
                                     m.GetParameters().Length >= 1 &&
                                     m.GetParameters()[0].ParameterType == typeof(string));
            MethodInfo postfix = typeof(GKFoodRebalancePlugin).GetMethod(nameof(LanguageLoadedPostfix), AnyStatic);
            if (target == null || postfix == null)
                throw new MissingMethodException("GJL.LoadLanguageResource patch target missing.");

            PatchMethod(PluginGuid + ".language", target, null, postfix);
        }

        private static void LanguageLoadedPostfix()
        {
            try
            {
                TryInjectLocalization();
            }
            catch (Exception ex)
            {
                if (Log != null) Log.LogWarning("Localization refresh failed: " + ex.Message);
            }
        }

        private static bool TryInjectLocalization()
        {
            Type gjlType = FindType("GJL");
            object currentLanguage = gjlType == null ? null : GetStaticMember(gjlType, "cur_lng");
            IDictionary dict = currentLanguage == null ? null : GetMember(currentLanguage, "dict") as IDictionary;
            if (dict == null) return false;

            string code = GetCurrentLanguageCode();
            string wellFedName;
            string wellFedDescription;
            GetWellFedTranslation(code, out wellFedName, out wellFedDescription);

            dict[WellFedBuffId] = wellFedName;
            dict[WellFedBuffId + "_d"] = wellFedDescription;
            dict[LegacyWellFedShortBuffId] = wellFedName;
            dict[LegacyWellFedShortBuffId + "_d"] = wellFedDescription;
            dict[LegacyWellFedLongBuffId] = wellFedName;
            dict[LegacyWellFedLongBuffId + "_d"] = wellFedDescription;

            string speedName = GetSpeedName(code);
            string speedDescription = GetDictionaryString(dict, "pot_speed_d");
            if (string.IsNullOrEmpty(speedDescription)) speedDescription = GetDictionaryString(dict, "buff_pot_speed_d");
            if (string.IsNullOrEmpty(speedDescription)) speedDescription = "Increase your speed.";
            dict[SpeedFoodBuffId] = speedName;
            dict[SpeedFoodBuffId + "_d"] = speedDescription;

            string inebriatedName;
            string inebriatedDescription;
            GetInebriatedTranslation(code, out inebriatedName, out inebriatedDescription);
            dict[InebriatedBuffId] = inebriatedName;
            dict[InebriatedBuffId + "_d"] = inebriatedDescription;

            string soberingName;
            string soberingDescription;
            GetSoberingTranslation(code, out soberingName, out soberingDescription);
            dict[SoberingFoodTriggerBuffId] = soberingName;
            dict[SoberingFoodTriggerBuffId + "_d"] = soberingDescription;

            if (!string.Equals(_lastInjectedLanguage, code, StringComparison.Ordinal))
            {
                _lastInjectedLanguage = code;
                if (Log != null)
                    Log.LogInfo("Injected buff localization for language '" + code + "' (generic Speed name; vanilla Speed description).");
            }

            return true;
        }

        private static string GetDictionaryString(IDictionary dict, string key)
        {
            if (dict == null || !dict.Contains(key)) return null;
            return dict[key] as string ?? Convert.ToString(dict[key], CultureInfo.InvariantCulture);
        }

        private static string GetCurrentLanguageCode()
        {
            Type settingsType = FindType("GameSettings");
            string code = settingsType == null ? null : GetStaticMember(settingsType, "_cur_lng") as string;
            if (string.IsNullOrEmpty(code)) return "en";
            return code.ToLowerInvariant().Replace('-', '_');
        }

        private static string GetSpeedName(string code)
        {
            switch (code)
            {
                case "de": return "Geschwindigkeit";
                case "es": return "Velocidad";
                case "fr": return "Vitesse";
                case "it": return "Velocità";
                case "ja": return "速度";
                case "ko": return "속도";
                case "pl": return "Szybkość";
                case "pt_br": return "Velocidade";
                case "ru": return "Скорость";
                case "zh_cn": return "速度";
                default: return "Speed";
            }
        }

        private static void GetWellFedTranslation(string code, out string name, out string description)
        {
            switch (code)
            {
                case "de":
                    name = "Gut genährt";
                    description = "Du arbeitest schneller an Produktionsstationen.";
                    return;
                case "es":
                    name = "Bien alimentado";
                    description = "Trabajas más rápido en las estaciones de producción.";
                    return;
                case "fr":
                    name = "Bien nourri";
                    description = "Vous travaillez plus vite aux postes de production.";
                    return;
                case "it":
                    name = "Ben nutrito";
                    description = "Lavori più velocemente alle postazioni di produzione.";
                    return;
                case "ja":
                    name = "満腹";
                    description = "生産設備での作業速度が上がる。";
                    return;
                case "ko":
                    name = "포만감";
                    description = "생산 작업대에서 더 빠르게 작업합니다.";
                    return;
                case "pl":
                    name = "Najedzony";
                    description = "Pracujesz szybciej przy stanowiskach produkcyjnych.";
                    return;
                case "pt_br":
                    name = "Bem alimentado";
                    description = "Você trabalha mais rápido nas estações de produção.";
                    return;
                case "ru":
                    name = "Сытость";
                    description = "Вы работаете на производственных станках быстрее.";
                    return;
                case "zh_cn":
                    name = "饱腹";
                    description = "在生产工作台上的工作速度更快。";
                    return;
                default:
                    name = "Well Fed";
                    description = "You work faster at production stations.";
                    return;
            }
        }

        private static void GetInebriatedTranslation(string code, out string name, out string description)
        {
            switch (code)
            {
                case "de": name = "Berauscht"; description = "Nach Alkohol bewegst du dich langsamer."; return;
                case "es": name = "Ebrio"; description = "Te mueves más despacio después de beber alcohol."; return;
                case "fr": name = "Ivre"; description = "Vous vous déplacez plus lentement après avoir bu de l'alcool."; return;
                case "it": name = "Ebbro"; description = "Ti muovi più lentamente dopo aver bevuto alcol."; return;
                case "ja": name = "酩酊"; description = "酒を飲んだ後は移動速度が低下する。"; return;
                case "ko": name = "취함"; description = "술을 마신 후 이동 속도가 느려집니다."; return;
                case "pl": name = "Nietrzeźwość"; description = "Po wypiciu alkoholu poruszasz się wolniej."; return;
                case "pt_br": name = "Embriagado"; description = "Você se move mais devagar depois de beber álcool."; return;
                case "ru": name = "Опьянение"; description = "После алкоголя вы двигаетесь медленнее."; return;
                case "zh_cn": name = "醉酒"; description = "饮酒后移动速度降低。"; return;
                default: name = "Inebriated"; description = "You move more slowly after drinking alcohol."; return;
            }
        }

        private static void GetSoberingTranslation(string code, out string name, out string description)
        {
            switch (code)
            {
                case "de": name = "Ausnüchterung"; description = "Verringert den Rausch."; return;
                case "es": name = "Despejarse"; description = "Reduce la embriaguez."; return;
                case "fr": name = "Dégrisement"; description = "Réduit l'ivresse."; return;
                case "it": name = "Smaltimento"; description = "Riduce l'ubriachezza."; return;
                case "ja": name = "酔い覚まし"; description = "酩酊を軽減する。"; return;
                case "ko": name = "해장"; description = "취기를 줄입니다."; return;
                case "pl": name = "Trzeźwienie"; description = "Zmniejsza nietrzeźwość."; return;
                case "pt_br": name = "Sobriedade"; description = "Reduz a embriaguez."; return;
                case "ru": name = "Отрезвление"; description = "Снижает опьянение."; return;
                case "zh_cn": name = "醒酒"; description = "减轻醉酒。"; return;
                default: name = "Sobering"; description = "Reduces Inebriated."; return;
            }
        }

        private static void InstallCraftSpeedPatch()
        {
            Type craftType = FindType("CraftComponent");
            Type worldGameObjectType = FindType("WorldGameObject");
            if (craftType == null) throw new InvalidOperationException("CraftComponent type missing.");
            if (worldGameObjectType == null) throw new InvalidOperationException("WorldGameObject type missing.");

            MethodInfo target = craftType.GetMethods(AnyInstance).FirstOrDefault(m =>
            {
                if (m.Name != "DoAction") return false;
                ParameterInfo[] p = m.GetParameters();
                return p.Length == 3 &&
                       p[0].ParameterType == worldGameObjectType &&
                       p[1].ParameterType == typeof(float) &&
                       p[2].ParameterType == typeof(bool);
            });
            MethodInfo prefix = typeof(GKFoodRebalancePlugin).GetMethod(nameof(CraftDoActionPrefix), AnyStatic);
            if (target == null || prefix == null)
                throw new MissingMethodException("CraftComponent.DoAction(WorldGameObject, float, bool) patch target missing.");

            PatchMethod(PluginGuid + ".speed", target, prefix, null);
        }

        private static void CraftDoActionPrefix(object __instance, object other_obj, ref float delta_time)
        {
            try
            {
                float deltaTimeIn = delta_time;
                object player;
                object currentCraft;
                string craftId;
                string diagnosticReason;
                string wgoId;
                bool eligible = TryGetManualPlayerCraft(
                    __instance,
                    other_obj,
                    out player,
                    out currentCraft,
                    out craftId,
                    out diagnosticReason,
                    out wgoId);

                if (!eligible)
                {
                    LogCraftDiagnostic(
                        player,
                        craftId,
                        wgoId,
                        false,
                        false,
                        false,
                        1.00f,
                        deltaTimeIn,
                        delta_time,
                        diagnosticReason);
                    return;
                }

                bool wellFed = IsWellFed(player);
                if (wellFed) delta_time *= WellFedCraftSpeedMultiplier;

                LogCraftDiagnostic(
                    player,
                    craftId,
                    wgoId,
                    true,
                    wellFed,
                    wellFed,
                    wellFed ? WellFedCraftSpeedMultiplier : 1.00f,
                    deltaTimeIn,
                    delta_time,
                    wellFed ? "well_fed_active" : "well_fed_inactive");
            }
            catch (Exception ex)
            {
                if (Log != null) Log.LogWarning("Well Fed craft-speed hook error: " + ex.Message);
            }
        }

        private static bool TryGetManualPlayerCraft(
            object craftComponent,
            object currentOtherObj,
            out object player,
            out object currentCraft,
            out string craftId,
            out string diagnosticReason,
            out string wgoId)
        {
            player = currentOtherObj;
            currentCraft = GetMember(craftComponent, "current_craft");
            craftId = currentCraft == null ? null : GetMember(currentCraft, "id") as string;

            object wgo = GetMember(craftComponent, "wgo");
            wgoId = (GetMember(wgo, "obj_id") as string) ?? string.Empty;
            string lower = wgoId.ToLowerInvariant();
            string excludedFragment = ExcludedCraftWgoFragments.FirstOrDefault(fragment => lower.Contains(fragment));

            if (player == null)
            {
                diagnosticReason = "actor_null";
                return false;
            }

            if (!GetBool(player, "is_player"))
            {
                diagnosticReason = "not_player";
                return false;
            }

            if (currentCraft == null)
            {
                diagnosticReason = excludedFragment == null ? "no_current_craft" : "excluded_wgo:" + excludedFragment;
                return false;
            }

            if (!string.IsNullOrEmpty(craftId) && craftId.Contains(":r:"))
            {
                diagnosticReason = "excluded_craft_id";
                return false;
            }

            if (excludedFragment != null)
            {
                diagnosticReason = "excluded_wgo:" + excludedFragment;
                return false;
            }

            diagnosticReason = "eligible";
            return true;
        }

        private static void LogCraftDiagnostic(
            object actor,
            string craftId,
            string wgoId,
            bool eligible,
            bool wellFed,
            bool applied,
            float multiplier,
            float deltaTimeIn,
            float deltaTimeOut,
            string reason)
        {
            if (Log == null) return;

            string actorType = actor == null ? "null" : actor.GetType().Name;
            string key =
                actorType + "|" +
                (craftId ?? string.Empty) + "|" +
                (wgoId ?? string.Empty) + "|" +
                eligible + "|" +
                wellFed + "|" +
                applied + "|" +
                (reason ?? string.Empty);

            lock (CraftDiagnosticSeen)
            {
                if (CraftDiagnosticSeen.Contains(key)) return;
                if (CraftDiagnosticSeen.Count >= MaxCraftDiagnosticEntries) return;
                CraftDiagnosticSeen.Add(key);
            }

            Log.LogInfo(
                "WELLFED_DIAGNOSTIC" +
                " actor=" + DiagnosticToken(actorType) +
                " craft=" + DiagnosticToken(craftId) +
                " wgo=" + DiagnosticToken(wgoId) +
                " eligible=" + eligible.ToString().ToLowerInvariant() +
                " well_fed=" + wellFed.ToString().ToLowerInvariant() +
                " applied=" + applied.ToString().ToLowerInvariant() +
                " multiplier=" + multiplier.ToString("0.00", CultureInfo.InvariantCulture) +
                " delta_in=" + deltaTimeIn.ToString("0.0000", CultureInfo.InvariantCulture) +
                " delta_out=" + deltaTimeOut.ToString("0.0000", CultureInfo.InvariantCulture) +
                " reason=" + DiagnosticToken(reason));
        }

        private static string DiagnosticToken(string value)
        {
            if (string.IsNullOrEmpty(value)) return "<none>";
            return value
                .Replace(' ', '_')
                .Replace('\t', '_')
                .Replace('\r', '_')
                .Replace('\n', '_');
        }

        private static bool IsWellFed(object player)
        {
            if (ReadNumericResource(player, WellFedResource) > 0f) return true;

            Type mainGameType = FindType("MainGame");
            object mainGame = mainGameType == null ? null : GetStaticMember(mainGameType, "me");
            object mainPlayer = GetMember(mainGame, "player");
            return ReadNumericResource(mainPlayer, WellFedResource) > 0f;
        }

        private static float ReadNumericResource(object obj, string resourceName)
        {
            if (obj == null) return 0f;

            string[] containers = { "res", "params", "parameters", "player_res", "player_params", "game_res" };
            for (int i = 0; i < containers.Length; i++)
            {
                float value;
                if (TryReadGameRes(GetMember(obj, containers[i]), resourceName, out value)) return value;
            }

            float direct;
            if (TryReadGameRes(obj, resourceName, out direct)) return direct;

            MethodInfo[] methods = obj.GetType().GetMethods(AnyInstance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string) || method.ReturnType == typeof(void)) continue;

                string name = method.Name.ToLowerInvariant();
                if (!(name.Contains("param") || name.Contains("resource") || name.Contains("res"))) continue;

                try
                {
                    float parsed;
                    if (TryToFloat(method.Invoke(obj, new object[] { resourceName }), out parsed)) return parsed;
                }
                catch { }
            }

            return 0f;
        }

        private static bool TryReadGameRes(object gameRes, string name, out float value)
        {
            value = 0f;
            if (gameRes == null) return false;
            IList types = GetMember(gameRes, "_res_type") as IList;
            IList values = GetMember(gameRes, "_res_v") as IList;
            if (types == null || values == null) return false;

            for (int i = 0; i < types.Count && i < values.Count; i++)
            {
                if (string.Equals(Convert.ToString(types[i], CultureInfo.InvariantCulture), name, StringComparison.Ordinal))
                    return TryToFloat(values[i], out value);
            }
            return false;
        }

        private static void SetGameResNamedValue(object gameRes, string name, float value)
        {
            IList types = GetMember(gameRes, "_res_type") as IList;
            IList values = GetMember(gameRes, "_res_v") as IList;
            if (types == null || values == null) return;

            for (int i = 0; i < types.Count && i < values.Count; i++)
            {
                if (string.Equals(Convert.ToString(types[i], CultureInfo.InvariantCulture), name, StringComparison.Ordinal))
                {
                    values[i] = ConvertForListElement(values, value);
                    return;
                }
            }

            types.Add(ConvertForListElement(types, name));
            values.Add(ConvertForListElement(values, value));
        }

        private static void PatchMethod(string harmonyId, MethodInfo target, MethodInfo prefix, MethodInfo postfix)
        {
            Type harmonyType = FindType("HarmonyLib.Harmony");
            Type harmonyMethodType = FindType("HarmonyLib.HarmonyMethod");
            if (harmonyType == null || harmonyMethodType == null)
                throw new InvalidOperationException("Harmony types missing.");

            object harmony = Activator.CreateInstance(harmonyType, new object[] { harmonyId });
            object harmonyPrefix = prefix == null ? null : Activator.CreateInstance(harmonyMethodType, new object[] { prefix });
            object harmonyPostfix = postfix == null ? null : Activator.CreateInstance(harmonyMethodType, new object[] { postfix });

            MethodInfo patch = harmonyType.GetMethods(AnyInstance)
                .FirstOrDefault(m => m.Name == "Patch" && m.GetParameters().Length >= 3 &&
                                     typeof(MethodBase).IsAssignableFrom(m.GetParameters()[0].ParameterType));
            if (patch == null) throw new MissingMethodException("Harmony.Patch missing.");

            object[] args = new object[patch.GetParameters().Length];
            args[0] = target;
            args[1] = harmonyPrefix;
            args[2] = harmonyPostfix;
            for (int i = 3; i < args.Length; i++) args[i] = null;
            patch.Invoke(harmony, args);
        }

        private static void ReplaceList(object obj, string memberName, IEnumerable<object> values)
        {
            object original = GetMember(obj, memberName);
            if (original == null) return;
            IList replacement = Activator.CreateInstance(original.GetType()) as IList;
            if (replacement == null) return;
            foreach (object value in values) replacement.Add(ConvertForListElement(replacement, value));
            SetMember(obj, memberName, replacement);
        }

        private static void CloneListMember(object obj, string memberName)
        {
            object original = GetMember(obj, memberName);
            IList source = original as IList;
            if (source == null) return;
            IList clone = Activator.CreateInstance(original.GetType()) as IList;
            if (clone == null) return;
            for (int i = 0; i < source.Count; i++) clone.Add(source[i]);
            SetMember(obj, memberName, clone);
        }

        private static object ConvertForListElement(IList list, object value)
        {
            Type listType = list.GetType();
            Type elementType = listType.IsGenericType ? listType.GetGenericArguments()[0] : (value == null ? typeof(object) : value.GetType());
            return ConvertForType(value, elementType);
        }

        private static object FindById(IList list, string id)
        {
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                object value = list[i];
                if (string.Equals(GetMember(value, "id") as string, id, StringComparison.Ordinal)) return value;
            }
            return null;
        }

        private static Type FindType(string name)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Assembly assembly = assemblies[a];
                try
                {
                    Type direct = assembly.GetType(name, false);
                    if (direct != null) return direct;
                    Type byName = assembly.GetTypes().FirstOrDefault(t => t.Name == name);
                    if (byName != null) return byName;
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Type byName = ex.Types.FirstOrDefault(t => t != null && t.Name == name);
                    if (byName != null) return byName;
                }
                catch { }
            }
            return null;
        }

        private static object GetMember(object obj, string name)
        {
            if (obj == null) return null;
            Type type = obj.GetType();
            FieldInfo field = type.GetField(name, AnyInstance);
            if (field != null) return field.GetValue(obj);
            PropertyInfo property = type.GetProperty(name, AnyInstance);
            return property != null && property.CanRead ? property.GetValue(obj, null) : null;
        }

        private static object GetStaticMember(Type type, string name)
        {
            if (type == null) return null;
            FieldInfo field = type.GetField(name, AnyStatic);
            if (field != null) return field.GetValue(null);
            PropertyInfo property = type.GetProperty(name, AnyStatic);
            return property != null && property.CanRead ? property.GetValue(null, null) : null;
        }

        private static bool GetBool(object obj, string name)
        {
            object value = GetMember(obj, name);
            if (value is bool) return (bool)value;
            bool parsed;
            return value != null && bool.TryParse(value.ToString(), out parsed) && parsed;
        }

        private static void SetMember(object obj, string name, object value)
        {
            if (obj == null) return;
            Type type = obj.GetType();
            FieldInfo field = type.GetField(name, AnyInstance);
            if (field != null)
            {
                field.SetValue(obj, ConvertForType(value, field.FieldType));
                return;
            }
            PropertyInfo property = type.GetProperty(name, AnyInstance);
            if (property != null && property.CanWrite)
                property.SetValue(obj, ConvertForType(value, property.PropertyType), null);
        }

        private static object ConvertForType(object value, Type targetType)
        {
            if (value == null) return null;
            Type nullable = Nullable.GetUnderlyingType(targetType);
            if (nullable != null) targetType = nullable;
            if (targetType.IsInstanceOfType(value)) return value;
            if (targetType.IsEnum) return Enum.Parse(targetType, value.ToString());
            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        private static object ShallowClone(object obj)
        {
            if (obj == null) return null;
            MethodInfo cloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            return cloneMethod.Invoke(obj, null);
        }

        private static void SetSmartExpression(object expression, string text)
        {
            if (expression == null) return;
            SetMember(expression, "_expression", text);
            SetMember(expression, "_exp", null);
            SetMember(expression, "_simplified", false);
            SetMember(expression, "_simpified_float", 0f);
            SetMember(expression, "default_value", 0f);
        }

        private static bool TryToFloat(object value, out float result)
        {
            result = 0f;
            if (value == null) return false;
            if (value is float) { result = (float)value; return true; }
            if (value is double) { result = (float)(double)value; return true; }
            if (value is int) { result = (int)value; return true; }
            return float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static string F(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
