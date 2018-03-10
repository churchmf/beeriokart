using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BeerioKartTournamentGenerator
{
    class Player
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public override string ToString()
        {
            return String.Format("{0}", Name);
        }
    }
    
    class Round
    {
        public int Id { get; set; }
        public List<Match> Matches { get; set; }

        public override string ToString()
        {
            return String.Format("Round [{0}]\n[{1}]", Id.ToString(), String.Join(Environment.NewLine, Matches));
        }
    }

    class Match
    {
        public int Id { get; set; }
        public List<Player> Players { get; set; }
        public DateTime Time { get; set; }

        public override string ToString()
        {
            return String.Format("Match [{0}] [{1}] [{2}]", Id.ToString(), Time.ToString("hh:mm tt"), String.Join(", ", Players));
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Variables
            const int MaxNumPlayersPerMatch = 4;
            const int NumRounds = 4;
            TimeSpan MatchLength = TimeSpan.FromMinutes(7);
            TimeSpan BreakBetweenRounds = TimeSpan.FromMinutes(5);
            DateTime StartTime = DateTime.Parse("3/10/2018 8:30 PM");
            string[] PlayerNames =
            {
                "Matt",
                "Lane",
                "Inga",
                "Kat",
                "Erin",
                "Angela",
                "Patrick",
                "Paul",
                "Jordan",
                "Monique",
                "Salish",
                "Brian",
                "Rayna",
                "Daryl",
                "Carlos",
                "Courtney"
            };

            var numPlayers = PlayerNames.Length;
            var players = new List<Player>();
            var rounds = new List<Round>();

            var playerMatchups = new Dictionary<string, int>();

            for (int playerIndex = 0; playerIndex < numPlayers; ++playerIndex) 
            {
                players.Add(new Player() { Id = playerIndex, Name = PlayerNames[playerIndex] });
            }

            for (int roundIndex = 0; roundIndex < NumRounds; ++roundIndex)
            {
                var matches = new List<Match>();

                var numMatchPerRound = Math.Ceiling((double)numPlayers / (double) MaxNumPlayersPerMatch);
                var unmatchedPlayers = new List<Player>(players);

                var roundStartTime = StartTime.AddMinutes((MatchLength.TotalMinutes * numMatchPerRound * (roundIndex)) + BreakBetweenRounds.TotalMinutes);

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
                                Time = roundStartTime.AddMinutes(MatchLength.TotalMinutes * matchIndex)
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

            Console.WriteLine(StartTime);
            File.WriteAllText("output.json", String.Join(Environment.NewLine, rounds));
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
