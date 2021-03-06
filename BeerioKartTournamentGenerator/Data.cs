﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BeerioKartTournamentGenerator
{
    [Serializable]
    public class Player
    {
        public int PlayerId { get; set; }

        public string Name { get; set; }

        [JsonIgnore]
        public float? HistoricalAveragePoints { get; set; }

        [JsonIgnore]
        public bool DisplayFractionalOdds { get; set; } = true;

        public Dictionary<string, object> Info { get; set; } = new Dictionary<string, object>();

        public override string ToString()
        {
            return String.Format("{0}", Name);
        }
    }

    [Serializable]
    public class Round
    {
        public int RoundId { get; set; }

        public List<Match> Matches { get; set; }

        public override string ToString()
        {
            return String.Format("Round [{0}]\n[{1}]", (RoundId + 1).ToString(), String.Join(Environment.NewLine, Matches));
        }
    }

    [Serializable]
    public class Match
    {
        public int MatchId { get; set; }

        public List<Player> Players { get; set; }

        public Dictionary<Player, float> FractionalOdds { get; set; }

        public DateTime Time { get; set; }

        public override string ToString()
        {
            string odds = String.Empty;
            if(FractionalOdds != null)
            {
                KeyValuePair<Player, float> underdog = FractionalOdds.Aggregate((x, y) => x.Value > y.Value ? x : y);
                KeyValuePair<Player, float> favored = FractionalOdds.Aggregate((x, y) => x.Value < y.Value ? x : y);
                odds = String.Format("[{0}]",
                        String.Join(", ", FractionalOdds
                        .Select(kvp => String.Format("{0} {2} {1}",
                            kvp.Key.Name,
                            Fraction.ParseFromReal(kvp.Value),
                            kvp.Key == favored.Key ? "(favoured)" : kvp.Key == underdog.Key ? "(underdog)" : String.Empty)
                        )
                    )
                );
            }

            return String.Format("Match [{0}] [{1}] [{2}] {3}",
                (MatchId + 1).ToString(),
                Time.ToString("hh:mm tt"),
                String.Join(", ", Players),
                odds
            );
        }
    }

}
