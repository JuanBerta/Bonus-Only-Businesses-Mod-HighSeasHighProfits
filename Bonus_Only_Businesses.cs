using HarmonyLib;
using MelonLoader;
using RockTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using zip.lexy.tgame.city;
using zip.lexy.tgame.constants;
using zip.lexy.tgame.random;
using zip.lexy.tgame.state;
using zip.lexy.tgame.state.building;
using zip.lexy.tgame.state.city;
using zip.lexy.tgame.state.city.mayor;
using zip.lexy.tgame.ui.gamegeneration;
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

        [HarmonyPatch(typeof(GenerateGame), "GetCityBonuses")]
        public static class GetCityBonuses_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(int seed, int numberOfCities, ref List<List<string>> __result)
            {
                // 1. Get our target count (e.g., 5)
                int count = Bonus_Only_Businesses_Class.MaxBonusCount;
                MelonLoader.MelonLogger.Msg($"Generating Regional Mandate: {count} bonuses per city.");

                // 2. Initialize the master list
                List<List<string>> masterList = new List<List<string>>(numberOfCities);
                zip.lexy.tgame.random.Random rand = RandGen.ByType(RandomType.CITY_BONUSES, seed);

                // 3. Create the "Deck" - Deal 'count' cards for every city
                List<string> deck = new List<string>(count * numberOfCities);
                for (int i = 0; i < count * numberOfCities; i++)
                {
                    deck.Add(Goods.ALL[i % Goods.ALL.Count]);
                }

                // 4. Shuffle the deck
                deck = ListUtils.Shuffle(rand, deck);

                // 5. Deal the cards into cities
                for (int j = 0; j < numberOfCities; j++)
                {
                    List<string> cityHand = new List<string>();
                    for (int k = 0; k < count; k++)
                    {
                        cityHand.Add(deck[j * count + k]);
                    }
                    masterList.Add(cityHand);
                }

                // 6. Set the result and skip the original method
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