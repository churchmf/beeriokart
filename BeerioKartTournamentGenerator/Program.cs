using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BeerioKartTournamentGenerator
{
    [HelpOption]
    public class Program
    {
        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        [Option(Description = "Maximum number of players per match", ShortName = "num-players")]
        public int MaxNumPlayersPerMatch { get; } = 4;

        [Option(Description = "Estimated match length in minutes", ShortName = "match-length")]
        public int MatchLength { get; } = 10;

        [Option(Description = "Number of rounds each player will play", ShortName = "num-rounds")]
        public int NumRounds { get; } = 4;

        [Option(Description = "Estimated break between rounds in minutes", ShortName = "break-length")]
        public int BreakBetweenRounds { get; } = 5;

        [Option(Description = "Start date and time of first match", ShortName = "start")]
        public string Start { get; }

        [Option(Description = "List of players. Defaults to players in players.json if not provided", ShortName = "players")]
        public string[] PlayerNames { get; set; }

        [Option(Description = "JSON file specifying player data", ShortName = "players-file")]
        public string PlayersFile { get; set; } = "players.json";

        void OnExecute()
        {
            var players = new List<Player>();
            var rounds = new List<Round>();
            var playerMatchups = new Dictionary<string, int>();

            DateTime startTime;
            if(!DateTime.TryParse(Start, out startTime))
            {
                Console.WriteLine("Defaulting to now. Unable to parse DateTime from command line");
                startTime = DateTime.Now;
            }

            if(PlayerNames != null && PlayerNames.Any())
            {
                Console.WriteLine("Found {0} players", PlayerNames.Length);
                for(int playerIndex = 0; playerIndex < PlayerNames.Length; ++playerIndex)
                {
                    players.Add(new Player() { Id = playerIndex, Name = PlayerNames[playerIndex] });
                }
            }
            else
            {
                if(File.Exists(PlayersFile))
                {
                    Console.WriteLine("Reading from players-file...");
                    players = JsonConvert.DeserializeObject<List<Player>>(File.ReadAllText(PlayersFile));
                }
                else
                {
                    throw new InvalidOperationException("No players provided. Please specify players or provide a valid players-file");
                }
            }

            // Initialize matchup counts
            for(int i = 0; i < players.Count; ++i)
            {
                for(int j = i + 1; j < players.Count; ++j)
                {
                    string key = GetKey(players[i], players[j]);
                    playerMatchups.Add(key, 0);
                }
            }

            for(int roundIndex = 0; roundIndex < NumRounds; ++roundIndex)
            {
                double numMatchPerRound = Math.Ceiling((double) players.Count / (double) MaxNumPlayersPerMatch);
                DateTime roundStartTime = startTime.AddMinutes((MatchLength * numMatchPerRound * (roundIndex)) + BreakBetweenRounds);

                Console.WriteLine("Generating matches for round {0}", roundIndex + 1);
                List<Match> matches = GenerateMatches(players, roundStartTime, playerMatchups);

                // Update player matchups
                foreach(Match match in matches)
                {
                    for(int i = 0; i < match.Players.Count; ++i)
                    {
                        for(int j = i + 1; j < match.Players.Count; ++j)
                        {
                            string matchup = GetKey(match.Players[i], match.Players[j]);
                            playerMatchups[matchup]++;
                        }
                    }
                }

                rounds.Add(new Round() { Id = roundIndex, Matches = matches });
            }

            string value = String.Join(Environment.NewLine, rounds);
            Console.WriteLine(value);
            File.WriteAllText("brackets.json", JsonConvert.SerializeObject(rounds, Formatting.Indented));
        }

        List<Player> GetMostUniqueMatch(List<Player> options, int matchSize, Dictionary<string, int> playerMatchups)
        {
            if(playerMatchups.All(p => p.Value == 0))
            {
                return options.Take(matchSize).ToList();
            }

            List<Player> mostUnique = null;
            int minScore = Int32.MaxValue;
            int maxScore = Int32.MinValue;
            int sumScore = 0;

            int numCombinations = 0;
            foreach(Player[] combination in Combinations.CombinationsRosettaWoRecursion(options.ToArray(), matchSize))
            {
                numCombinations++;
                int score = CalculateUniquenessScore(combination.ToList(), playerMatchups);
                if(score <= minScore)
                {
                    minScore = score;
                    mostUnique = combination.ToList();
                }
                if(score >= maxScore)
                {
                    maxScore = score;
                }
                sumScore += score;
            }

            Console.WriteLine("Checked {0} combinations", numCombinations);
            Console.WriteLine("Most Unique {0}", String.Join(", ", mostUnique));
            Console.WriteLine("Min Score {0}", minScore);
            Console.WriteLine("Max Score {0}", maxScore);
            Console.WriteLine("Average Score {0}", (float) sumScore / numCombinations);
            return mostUnique;
        }

        List<Match> GenerateMatches(List<Player> players, DateTime roundStartTime, Dictionary<string, int> playerMatchups)
        {
            var matches = new List<Match>();

            var remainingPlayers = new List<Player>(players);
            remainingPlayers.Shuffle();
            while(remainingPlayers.Count > 0)
            {
                // If we don't have even brackets, we need to split the remainder across other brackets
                int numPlayersInBracket = MaxNumPlayersPerMatch;
                if(remainingPlayers.Count % MaxNumPlayersPerMatch != 0)
                {
                    numPlayersInBracket = Math.Max(0, MaxNumPlayersPerMatch - 1);
                }

                List<Player> bestPlayerMatchup = GetMostUniqueMatch(remainingPlayers, numPlayersInBracket, playerMatchups);
                foreach(Player player in bestPlayerMatchup)
                {
                    remainingPlayers.Remove(player);
                }

                matches.Add(new Match() { Id = matches.Count, Players = bestPlayerMatchup, Time = roundStartTime.AddMinutes(MatchLength * matches.Count) });
            }

            // Calculate fractional odds for players in each match
            foreach(Match match in matches)
            {
                match.FractionalOdds = new Dictionary<Player, float>();

                float sumPoints = match.Players.Sum(p => p.HistoricalAveragePoints.GetValueOrDefault());
                if(sumPoints > 0f)
                {
                    foreach(Player player in match.Players.Where(p => p.DisplayFractionalOdds))
                    {
                        match.FractionalOdds.Add(player, ConvertProbabilityToFractionalOdds(player.HistoricalAveragePoints.GetValueOrDefault() / sumPoints));
                    }
                }
            }

            return matches;
        }

        int CalculateUniquenessScore(List<Player> players, Dictionary<string, int> playerMatchups)
        {
            int max = Int32.MinValue;
            int min = Int32.MaxValue;
            int sum = 0;
            for(int i = 0; i < players.Count; ++i)
            {
                for(int j = i + 1; j < players.Count; ++j)
                {
                    Player a = players[i];
                    Player b = players[j];
                    int count = 0;

                    string key = GetKey(a, b);
                    playerMatchups.TryGetValue(key, out count);

                    if(count > max)
                    {
                        max = count;
                    }

                    if(count < min)
                    {
                        min = count;
                    }

                    sum += count;
                }
            }
            return sum + (max + min * 2);
        }

        // Hash of unique player matchings
        string GetKey(Player a, Player b)
        {
            var ordered = new int[] { a.Id, b.Id };
            return String.Join("-", ordered.OrderBy(i => i));
        }

        // Probability = Fractional odds denominator / (Fractional odds denominator + Fractional odds numerator)
        float ConvertProbabilityToFractionalOdds(float probability)
        {
            return (1f - probability) / probability;
        }
    }
}
