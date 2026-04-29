using HarmonyLib;
using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using zip.lexy.tgame.constants;
using zip.lexy.tgame.random;
using zip.lexy.tgame.state;
using zip.lexy.tgame.state.city;
using zip.lexy.tgame.state.city.mayor;
using zip.lexy.tgame.ui.gamegeneration;
using zip.lexy.tgame.ui.settings;
using zip.lexy.tgame.ui.widget.build;
using zip.lexy.tgame.ui.widget.trade;
using zip.lexy.tgame.util;

namespace Bonus_Only_Businesses
{
    public class Bonus_Only_Businesses_Class : MelonMod
    {
        public static int MaxBonusCount = 5;

        [HarmonyPatch(typeof(BuildWindow))]
        public static class UniversalFilterPatch
        {
            // This targets all the filter methods at once
            [HarmonyPrefix]
            [HarmonyPatch("FilterRaw")]
            [HarmonyPatch("FilterProcessed")]
            [HarmonyPatch("FilterShip")]
            [HarmonyPatch("FilterFood")]
            [HarmonyPatch("FilterAll")]
            public static bool Prefix(BuildWindow __instance, System.Reflection.MethodBase __originalMethod)
            {
                // 1. Get the city via Traverse
                var gameState = Traverse.Create(__instance).Property("gameState").GetValue<GameState>();
                if (gameState == null || gameState.viewCity == null) return true;

                City city = gameState.viewCity;

                // 2. Determine which list to use based on which method was called
                // We use the stack trace to see which "Filter" button was pressed
                string methodName = __originalMethod.Name;

                List<string> categoryList;
                switch (methodName)
                {
                    case "FilterRaw": categoryList = Traverse.Create(__instance).Field("RAW").GetValue<List<string>>(); break;
                    case "FilterProcessed": categoryList = Traverse.Create(__instance).Field("PROCESSED").GetValue<List<string>>(); break;
                    case "FilterShip": categoryList = Traverse.Create(__instance).Field("SHIP").GetValue<List<string>>(); break;
                    case "FilterFood": categoryList = Traverse.Create(__instance).Field("FOOD").GetValue<List<string>>(); break;
                    default: categoryList = Goods.ALL; break;
                }

                // 3. Filter that category by the city bonuses
                List<string> filteredList = categoryList.Where(g => city.bonuses.Contains(g)).ToList();

                // 4. Manually trigger the display with our filtered category
                __instance.ShowBusinessesForGoods(filteredList);

                // 5. Return false to skip the original (unfiltered) method
                return false;
            }
        }

        [HarmonyPatch(typeof(GoalGiver), "SetupBusinessGoalFromGoodType")]
        public static class GoalGiver_Fix_Patch
        {
            // We use a Prefix with 'ref string goodType' to change the actual input
            public static void Prefix(ref string goodType)
            {
                // 1. Get the current city context
                // In GoalGiver, we usually rely on the GameState's viewCity or the active city
                var gameState = InstanceProvider.GetInstance<GameState>();
                if (gameState == null || gameState.viewCity == null) return;

                City city = gameState.viewCity;

                // 2. Check if the requested good is illegal (not a bonus)
                if (!city.bonuses.Contains(goodType))
                {
                    // 3. If it's illegal, pick a random LEGAL bonus good instead
                    if (city.bonuses.Count > 0)
                    {
                        // We use Unity's Random or the game's custom one
                        int index = UnityEngine.Random.Range(0, city.bonuses.Count);
                        string oldGood = goodType;
                        goodType = city.bonuses[index];

                        MelonLoader.MelonLogger.Msg($"Mayor tried to start a project for {oldGood}. I convinced them to build {goodType} instead!");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(GoalGiver), "ParametrizeNewBusinessGoal")]
        public static class GoalGiver_Parametrize_Patch
        {
            // We return 'false' to skip the original 30/70 logic entirely
            public static bool Prefix(MayorGoal goal, Mayor mayor)
            {
                // 1. Get the list of legal bonuses for this specific city
                List<string> bonuses = mayor.city.bonuses;

                if (bonuses == null || bonuses.Count == 0)
                {
                    MelonLoader.MelonLogger.Error($"City {mayor.city.name} has no bonuses! Mayor is confused.");
                    return true; // Fallback to original if something is wrong
                }

                // 2. Pick a random specialized good (100% chance)
                // We use the game's seed for consistency, just like the original code did
                GameState state = InstanceProvider.GetInstance<GameState>();
                zip.lexy.tgame.random.Random uniqueRandom = RandGen.GetUniqueRandom(RandomType.MAYOR_GOALS, state.seed);

                string goodToBuild = bonuses[uniqueRandom.Next(bonuses.Count)];

                // 3. Manually call the method that sets the goal details
                // Since we are skipping the original method, we must do this part ourselves
                Traverse.Create(typeof(GoalGiver))
                    .Method("AddBusinessGoalDetailsFromType", new object[] { goal, goodToBuild })
                    .GetValue();

                MelonLoader.MelonLogger.Msg($"Regional Mandate: Mayor of {mayor.city.name} is starting a project for specialized {goodToBuild}.");

                return false; // Skip the original method so it doesn't pick an 'illegal' good
            }
        }

        [HarmonyPatch(typeof(GeneralSettingsWindow), "Start")]
        public static class GeneralSettings_UI_Injection_Patch
        {
            public static void Postfix(GeneralSettingsWindow __instance)
            {
                Transform windowTransform = __instance.transform.Find("window");
                Transform languageTransform = windowTransform?.Find("language");
                if (languageTransform == null) return;

                // 1. Clone the row
                GameObject specRow = Object.Instantiate(languageTransform.gameObject, windowTransform);
                specRow.name = "specialization_setting";

                // 2. Adjust Position (Shift it down so it's under Language)
                specRow.transform.localPosition += new Vector3(0, -70, 0);

                // 3. Setup the Label
                TMPro.TextMeshProUGUI label = specRow.transform.Find("label").GetComponent<TMPro.TextMeshProUGUI>();
                label.text = "Regional Specialization";

                // 4. Setup the Dropdown
                TMPro.TMP_Dropdown dropdown = specRow.transform.Find("dropdown").GetComponent<TMPro.TMP_Dropdown>();

                // CRITICAL: Remove the game's default "ChangeLanguage" listener
                dropdown.onValueChanged = new TMPro.TMP_Dropdown.DropdownEvent();

                dropdown.options.Clear();
                for (int i = 1; i <= 10; i++)
                {
                    dropdown.options.Add(new TMPro.TMP_Dropdown.OptionData { text = $"{i} Bonuses" });
                }

                // 5. Load/Save Logic
                int currentSaved = PlayerPrefs.GetInt("mod.regional_mandate.bonus_count", 5);
                dropdown.SetValueWithoutNotify(currentSaved - 1);

                dropdown.onValueChanged.AddListener((int val) => {
                    int count = val + 1;
                    PlayerPrefs.SetInt("mod.regional_mandate.bonus_count", count);

                    // Re-assert the label text in case the game tries to overwrite it again
                    label.text = "Regional Specialization";

                    MelonLoader.MelonLogger.Msg($"Mandate Updated: {count} bonuses.");
                });
            }
        }

        [HarmonyPatch(typeof(GenerateGame), "GetCityBonuses")]
        public static class GetCityBonuses_Final_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(int seed, int numberOfCities, ref List<List<string>> __result)
            {
                int countPerCity = PlayerPrefs.GetInt("mod.regional_mandate.bonus_count", 3);
                int totalBonusSlots = countPerCity * numberOfCities;

                List<List<string>> masterList = new List<List<string>>(numberOfCities);
                for (int i = 0; i < numberOfCities; i++) masterList.Add(new List<string>());

                zip.lexy.tgame.random.Random rand = RandGen.ByType(RandomType.CITY_BONUSES, seed);

                // 1. Create a "Global Requirement Deck"
                // This ensures every good in the game appears at least once.
                List<string> globalDeck = new List<string>();

                // Add one of every good first
                globalDeck.AddRange(Goods.ALL);

                // Fill the rest of the deck with random goods until we have enough for all cities
                while (globalDeck.Count < totalBonusSlots)
                {
                    globalDeck.Add(Goods.ALL[rand.Next(Goods.ALL.Count)]);
                }

                // 2. Shuffle the global deck
                globalDeck = ListUtils.Shuffle(rand, globalDeck);

                // 3. Distribute the deck to cities, ensuring NO DUPLICATES per city
                int deckIndex = 0;
                for (int cityIdx = 0; cityIdx < numberOfCities; cityIdx++)
                {
                    int safetyNet = 0;
                    while (masterList[cityIdx].Count < countPerCity && safetyNet < 500)
                    {
                        safetyNet++;
                        string candidateGood = globalDeck[deckIndex % globalDeck.Count];
                        deckIndex++;

                        // If the city doesn't have this good yet, add it
                        if (!masterList[cityIdx].Contains(candidateGood))
                        {
                            masterList[cityIdx].Add(candidateGood);
                        }
                        // If it IS a duplicate for this city, the loop continues 
                        // and we'll pull the next card from the deck for this city instead.
                    }
                }

                MelonLoader.MelonLogger.Msg($"World generated with {countPerCity} bonuses per city. All goods guaranteed to exist.");
                __result = masterList;
                return false;
            }
        }

        [HarmonyPatch(typeof(GameState), "SetInitialCities")]
        public static class Initial_World_Sanitization_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(GameState __instance)
            {
                MelonLoader.MelonLogger.Msg("Regional Mandate: Removing wrong Business...");

                foreach (City city in __instance.cities)
                {
                    // The game just finished generating the world. 
                    // We check every building and delete those without a matching bonus.
                    int count = city.buildings.RemoveAll(b =>
                        b.type >= 1 && b.type <= 21 && !city.bonuses.Contains(GetGoodFromID(b.type))
                    );

                    if (count > 0)
                    {
                        MelonLoader.MelonLogger.Msg($"Cleaned {count} illegal buildings from {city.name} at world start.");
                    }
                }
            }
        }

        // Reuse your GetGoodFromID helper here
        private static string GetGoodFromID(int id)
        {
            switch (id)
            {
                case 1: return "ale";
                case 2: return "beef";
                case 3: return "bricks";
                case 4: return "clay";
                case 5: return "cloth";
                case 6: return "fish";
                case 7: return "grain";
                case 8: return "honey";
                case 9: return "iron-bars";
                case 10: return "logs";
                case 11: return "lumber";
                case 12: return "mead";
                case 13: return "ore";
                case 14: return "pottery";
                case 15: return "salt";
                case 16: return "stone";
                case 17: return "tar";
                case 18: return "vegetables";
                case 19: return "wine";
                case 20: return "wooden-tools";
                case 21: return "wool";
                default: return null;
            }
        }
    }
}