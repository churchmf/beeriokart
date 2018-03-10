using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            DateTime StartTime = DateTime.Parse("2/25/2018 8:30 PM");
            string[] PlayerNames =
            {
                "Lane",
                "Inga",
                "Matt",
                "Erin",
                "Angela",
                "Katie",
                "Alex McCoid",
                "Carlos",
                "Lauren",
                "Pat",
                "Jordan",
                "Cam",
                "Monique",
                "Kim",
                "Salish",
                "Brian",
                "Rayna",
                "Daryl",
                "AJ",
                "Kevin",
                "Ben",
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

                        var player = GetPlayerForMatch(unmatchedPlayers, match.Players, playerMatchups);
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

        public static Player GetPlayerForMatch(List<Player> unmatchedPlayers, List<Player> matchPlayers, Dictionary<string, int> playerMatchups, int maxCount = 0)
        {
            Player playerForMatch = null;
            // shuffle un matched players to try and get better pairings
            unmatchedPlayers = unmatchedPlayers.OrderBy(a => Guid.NewGuid()).ToList();
            foreach (Player eligiblePlayer in unmatchedPlayers)
            {
                var eligible = true;
                foreach (Player matchPlayer in matchPlayers)
                {
                    int count = 0;
                    if(playerMatchups.TryGetValue(GetKey(eligiblePlayer, matchPlayer), out count))
                    {
                        if (count > maxCount)
                        {
                            eligible = false;
                            break;
                        }
                    }
                }

                if(eligible)
                {
                    playerForMatch = eligiblePlayer;
                    break;
                }
            }

            if(playerForMatch == null && unmatchedPlayers.Count > 0)
            {
                // We didn't find a player, so ignore match history and shuffle list
                playerForMatch = GetPlayerForMatch(unmatchedPlayers, matchPlayers, playerMatchups, playerMatchups.Max(m=>m.Value));
            }

            return playerForMatch;
        }

        public static string GetKey(Player a, Player b)
        {
            var ordered = new int[] { a.Id, b.Id };
            return String.Join("-", ordered.OrderBy(i => i));
        }
    }
}
