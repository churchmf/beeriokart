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

            for(int roundIndex = 0; roundIndex < NumRounds; ++roundIndex)
            {
                var matches = new List<Match>();

                double numMatchPerRound = Math.Ceiling((double) players.Count / (double) MaxNumPlayersPerMatch);
                DateTime roundStartTime = startTime.AddMinutes((MatchLength * numMatchPerRound * (roundIndex)) + BreakBetweenRounds);

                var unmatchedPlayers = new List<Player>(players);
                while(unmatchedPlayers.Count > 0)
                {
                    for(int matchIndex = 0; matchIndex < numMatchPerRound; ++matchIndex)
                    {
                        Match match = matches
                            .Where(ma => ma.Id == matchIndex)
                            .SingleOrDefault();
                        if(match == null)
                        {
                            match = new Match()
                            {
                                Id = matchIndex,
                                Players = new List<Player>(),
                                Time = roundStartTime.AddMinutes(MatchLength * matchIndex)
                            };
                            matches.Add(match);
                        }

                        // Get the best (most unique matchup) player from unmatched based on previous match ups
                        Player player = unmatchedPlayers
                            .OrderBy(p => GetPlayerMatchUpSum(p, match.Players, playerMatchups))
                            .First();
                        if(player != null)
                        {
                            unmatchedPlayers.Remove(player);
                            match.Players.Add(player);
                        }
                    }
                }

                // update player matchups
                foreach(Match match in matches)
                {
                    for(int i = 0; i < match.Players.Count; ++i)
                    {
                        for(int j = i + 1; j < match.Players.Count; ++j)
                        {
                            string matchup = GetKey(match.Players[i], match.Players[j]);
                            int count;
                            if(!playerMatchups.TryGetValue(matchup, out count))
                            {
                                playerMatchups[matchup] = 0;
                            }
                            playerMatchups[matchup]++;
                        }
                    }
                }

                // Calculate fractional odds for players in each match
                foreach(Match match in matches)
                {
                    match.FractionalOdds = new Dictionary<Player, float>();

                    float sumPoints = match.Players.Sum(p => p.HistoricalAveragePoints);
                    foreach(Player player in match.Players)
                    {
                        match.FractionalOdds.Add(player, ConvertProbabilityToFractionalOdds(player.HistoricalAveragePoints / sumPoints));
                    }
                }

                rounds.Add(new Round() { Id = roundIndex, Matches = matches });
            }

            Console.WriteLine(String.Join(Environment.NewLine, rounds));
            File.WriteAllText("brackets.json", JsonConvert.SerializeObject(rounds, Formatting.Indented));
        }

        int GetPlayerMatchUpSum(Player eligiblePlayer, List<Player> matchPlayers, Dictionary<string, int> playerMatchups)
        {
            int sum = 0;
            foreach(Player matchPlayer in matchPlayers)
            {
                int count = 0;
                if(playerMatchups.TryGetValue(GetKey(eligiblePlayer, matchPlayer), out count))
                {
                    sum += count;
                }
            }
            return sum;
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
