using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;

namespace BeerioKartTournamentGenerator
{
    public class Player
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public override string ToString()
        {
            return String.Format("{0}", Name);
        }
    }
    
    public class Round
    {
        public int Id { get; set; }
        public List<Match> Matches { get; set; }

        public override string ToString()
        {
            return String.Format("Round [{0}]\n[{1}]", (Id + 1).ToString(), String.Join(Environment.NewLine, Matches));
        }
    }

    public class Match
    {
        public int Id { get; set; }
        public List<Player> Players { get; set; }
        public DateTime Time { get; set; }

        public override string ToString()
        {
            return String.Format("Match [{0}] [{1}] [{2}]", (Id + 1).ToString(), Time.ToString("hh:mm tt"), String.Join(", ", Players));
        }
    }

    [HelpOption]
    public class Program
    {
        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        [Required]
        [Option(Description = "Maximum number of players per match", ShortName = "num-players")]
        public int MaxNumPlayersPerMatch { get; }

        [Required]
        [Option(Description = "Number of rounds each player will play", ShortName = "num-rounds")]
        public int NumRounds { get; }

        [Required]
        [Option(Description = "Estimated match length in minutes", ShortName = "match-length")]
        public int MatchLength { get; }

        [Required]
        [Option(Description = "Estimated break between rounds in minutes", ShortName = "break-length")]
        public int BreakBetweenRounds { get; }

        [Option(Description = "Start date and time of first match", ShortName = "start")]
        public string Start { get; }

        [Option(Description = "List of players. Defaults to players in players.json if not provided", ShortName = "players")]
        public string[] Players { get; set; }

        public const string PlayersFile = "players.json";

        private void OnExecute()
        {
            if(!Players.Any() && File.Exists(PlayersFile))
            {
                Console.WriteLine("No Players provided, reading from players.json instead...");
                Players = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(PlayersFile));
                Console.WriteLine("Found {0} players", Players.Length);
            }

            DateTime startTime;
            if(!DateTime.TryParse(Start, out startTime) )
            {
                Console.WriteLine("Defaulting to now. Unable to parse DateTime from " + Start);
                startTime = DateTime.Now;
            }

            var numPlayers = Players.Length;
            var players = new List<Player>();
            var rounds = new List<Round>();

            var playerMatchups = new Dictionary<string, int>();

            for (int playerIndex = 0; playerIndex < numPlayers; ++playerIndex) 
            {
                players.Add(new Player() { Id = playerIndex, Name = Players[playerIndex] });
            }

            for (int roundIndex = 0; roundIndex < NumRounds; ++roundIndex)
            {
                var matches = new List<Match>();

                var numMatchPerRound = Math.Ceiling((double)numPlayers / (double) MaxNumPlayersPerMatch);
                var unmatchedPlayers = new List<Player>(players);

                var roundStartTime = startTime.AddMinutes((MatchLength * numMatchPerRound * (roundIndex)) + BreakBetweenRounds);

                while (unmatchedPlayers.Count > 0)
                {
                    for (int matchIndex = 0; matchIndex < numMatchPerRound; ++matchIndex)
                    {
                        var match = matches.Where(ma => ma.Id == matchIndex).SingleOrDefault();
                        if(match == null)
                        {
                            match = new Match() {
                                Id = matchIndex,
                                Players = new List<Player>(),
                                Time = roundStartTime.AddMinutes(MatchLength * matchIndex)
                            };
                            matches.Add(match);
                        }

                        // Get the best (most unique matchup) player from unmatched based on previous match ups
                        var player = unmatchedPlayers.OrderBy(p => GetPlayerMatchUpSum(p, match.Players, playerMatchups)).First();
                        if (player != null)
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
                        for(int j = i+1; j < match.Players.Count; ++j)
                        {
                            var matchup = GetKey(match.Players[i], match.Players[j]);
                            int count;
                            if (!playerMatchups.TryGetValue(matchup, out count))
                            {
                                playerMatchups[matchup] = 0;
                            }
                            playerMatchups[matchup]++;
                        }
                    }
                }

                rounds.Add(new Round() { Id = roundIndex, Matches = matches });
            }
            var maxMatchups = playerMatchups.Max(m => m.Value);
            var minMatchups = playerMatchups.Min(m => m.Value);

            Console.WriteLine(String.Join(Environment.NewLine, rounds));
            File.WriteAllText("brackets.json", JsonConvert.SerializeObject(rounds));
        }

        public static int GetPlayerMatchUpSum(Player eligiblePlayer, List<Player> matchPlayers, Dictionary<string, int> playerMatchups)
        {
            int sum = 0;
            foreach(Player matchPlayer in matchPlayers)
            {
                int count = 0;
                if (playerMatchups.TryGetValue(GetKey(eligiblePlayer, matchPlayer), out count))
                {
                    sum += count;
                }
            }
            return sum;
        }

        // Hash of unique player matchings
        public static string GetKey(Player a, Player b)
        {
            var ordered = new int[] { a.Id, b.Id };
            return String.Join("-", ordered.OrderBy(i => i));
        }
    }
}
