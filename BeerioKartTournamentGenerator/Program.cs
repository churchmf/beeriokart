using NDesk.Options;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BeerioKartTournamentGenerator
{
    public class Program
    {
        static Logger s_logger = LogManager.GetCurrentClassLogger();

        int _maxNumPlayersPerMatch = 4;
        int _matchLength = 10;
        int _numRounds = 4;
        int _breakBetweenRounds = 5;
        DateTime _start = DateTime.Now;
        string _playersFile = "players.json";
        string _bracketsFile = "brackets.json";
        bool _calculateOdds = false;
        LogLevel _minConsoleLogLevel = LogLevel.Info;

        public static int Main(string[] args)
        {
            var program = new Program();
            program.ParseOptions(args);

            try
            {
                program.Execute();
            }
            catch(Exception e)
            {
                s_logger.Error(e);
                return 1;
            }

            return 0;
        }

        void ParseOptions(string[] args)
        {
            bool showHelp = false;

            var options = new OptionSet() {
                {
                        "n|num-players",
                        "Maximum number of players per match",
                        (int v) => _maxNumPlayersPerMatch = v
                },
                {
                        "l|match-length",
                        "Expected match length in minutes",
                        (int v) => _matchLength = v
                },
                {
                        "r|num-rounds",
                        "Number of rounds each player will play",
                        (int v) => _numRounds = v
                },
                {
                        "b|break-length",
                        "Estimated break between rounds in minutes",
                        (int v) => _breakBetweenRounds = v
                },
                {
                        "s|start",
                        "Start date time of first match",
                        v => _start = DateTime.Parse(v)
                },
                {
                        "p|players",
                        "Path to players JSON file",
                        v => _playersFile = v
                },
                {
                        "o|output",
                        "Path for brackets JSON output file",
                        v => _bracketsFile = v
                },
                {
                        "c|calculate-odds",
                        "Calculate bracket fractional odds",
                        v => { if(v != null) _calculateOdds = true; }
                },
                {
                        "v",
                        "increase console message verbosity",
                        v => { if (v != null)  _minConsoleLogLevel = LogLevel.Debug; }
                },
                {
                        "h|help",
                        "Information about using this application",
                        v => showHelp = v != null
                },
            };

            try
            {
                options.Parse(args);
            }
            catch(OptionException e)
            {
                Console.WriteLine(e.Message);
                showHelp = true;
            }

            if(showHelp)
            {
                options.WriteOptionDescriptions(Console.Out);
            }

            ConfigureLogging();
        }

        void ConfigureLogging()
        {
            var config = new NLog.Config.LoggingConfiguration();
            var logfile = new NLog.Targets.FileTarget("logfile")
            {
                FileName = String.Format("{0}.log", AppDomain.CurrentDomain.FriendlyName)
            };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            config.AddRule(_minConsoleLogLevel, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
            LogManager.Configuration = config;
        }

        void Execute()
        {
            var players = new List<Player>();
            var rounds = new List<Round>();
            var playerMatchups = new Dictionary<string, int>();

            if(File.Exists(_playersFile))
            {
                players = JsonConvert.DeserializeObject<List<Player>>(File.ReadAllText(_playersFile));
            }
            else
            {
                throw new InvalidOperationException("No players provided. Please specify players or provide a valid players-file");
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

            for(int roundIndex = 0; roundIndex < _numRounds; ++roundIndex)
            {
                double numMatchPerRound = Math.Ceiling((double) players.Count / (double) _maxNumPlayersPerMatch);
                DateTime roundStartTime = _start.AddMinutes((_matchLength * numMatchPerRound * (roundIndex)) + _breakBetweenRounds);

                s_logger.Debug("Generating matches for round {0}", roundIndex + 1);
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

            s_logger.Info(String.Join(Environment.NewLine, rounds));
            File.WriteAllText(_bracketsFile, JsonConvert.SerializeObject(
                rounds,
                Formatting.Indented,
                new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore }
                )
            );
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

            s_logger.Debug("Checked {0} combinations", numCombinations);
            s_logger.Debug("Min Score {0}", minScore);
            s_logger.Debug("Max Score {0}", maxScore);
            s_logger.Debug("Average Score {0}", (float) sumScore / numCombinations);
            s_logger.Debug("Most Unique {0}", String.Join(", ", mostUnique));
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
                int numPlayersInBracket = _maxNumPlayersPerMatch;
                if(remainingPlayers.Count % _maxNumPlayersPerMatch != 0)
                {
                    numPlayersInBracket = Math.Max(0, _maxNumPlayersPerMatch - 1);
                }

                List<Player> bestPlayerMatchup = GetMostUniqueMatch(remainingPlayers, numPlayersInBracket, playerMatchups);
                foreach(Player player in bestPlayerMatchup)
                {
                    remainingPlayers.Remove(player);
                }

                matches.Add(new Match() { Id = matches.Count, Players = bestPlayerMatchup, Time = roundStartTime.AddMinutes(_matchLength * matches.Count) });
            }

            // Calculate fractional odds for players in each match
            if(_calculateOdds)
            {
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
