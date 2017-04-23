using System;
using System.Diagnostics;

namespace LimitOrderBookUtilities
{
    /// <summary>
    /// Simple porgress bar for command line tools
    /// </summary>
    public class ProgressBar : IDisposable
    {
        #region Properties

        private string Info { set; get; }
        private double Total { set; get; }
        private double EstimatedResidualTime { set; get; }
        private Stopwatch Stopwatch { set; get; }
        private long Count { set; get; }
        private double LastProgress { set; get; }
        private double PercentResolution { set; get; }

        #endregion Properties

        #region Constructor

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="total"></param>
        /// <param name="info"></param>
        /// <param name="percentResolution"></param>
        public ProgressBar(double total, string info, double percentResolution = 1)
        {
            Total = total;
            Info = info;
            Count++;
            Stopwatch = new Stopwatch(); 
            Stopwatch.Start();
            EstimatedResidualTime = 0;
            LastProgress = 0;
            PercentResolution = percentResolution;
        }

        #endregion Constructor

        #region Methods

        public void AdjustTotal(long total)
        {
            Total = total;
        }

        public void AdjustTotal(double total)
        {
            Total = total;
        }

        /// <summary>
        /// Report progress
        /// </summary>
        /// <param name="done"></param>
        public void Tick(double done)
        {
            var progress = (int)(100 * done / Total);

            if (progress - LastProgress > PercentResolution)
            {
                LastProgress = progress;

                Count++;
                Stopwatch.Stop();
                if (Math.Abs(done) < 1e-10)
                {
                    EstimatedResidualTime = 0;
                }
                else
                {
                    var duration = Stopwatch.ElapsedMilliseconds / 1000.0;
                    EstimatedResidualTime = (Total / done - 1.0) * duration;
                }
                Stopwatch.Start();

                DrawTextProgressBar(progress, 100);
            }

        }

        /// <summary>
        /// Finished progress
        /// </summary>
        public void Finished()
        {
            DrawTextProgressBar(100, 100);
            Console.WriteLine();
        }

        /// <summary>
        /// Draw progress bar in console 
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="total"></param>
        private void DrawTextProgressBar(int progress, int total)
        {
            //draw empty progress bar
            Console.CursorLeft = 0;
            Console.Write("["); //start
            Console.CursorLeft = 32;
            Console.Write("]"); //end
            Console.CursorLeft = 1;

            var onechunk = 30.0f / total;

            //draw filled part
            var position = 1;
            for (var i = 0; i <= onechunk * progress; i++)
            {
                Console.BackgroundColor = ConsoleColor.DarkMagenta;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }

            //draw unfilled part
            for (var i = position; i <= 31; i++)
            {
                Console.BackgroundColor = ConsoleColor.Magenta;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }

            //draw totals
            Console.CursorLeft = 35;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write("{0}: residual time: {1}                                                  ",
                Info, DurationInAppropriateUnits(EstimatedResidualTime)); //blanks at the end remove any excess



        }

        /// <summary>
        /// Show time in appropriate units 
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private static string DurationInAppropriateUnits(double t)
        {
            if (t > 24 * 60 * 60)
            {
                return $"{t/60.0/60.0/24.0:##.0} days";
            }
            else if (t > 60 * 60)
            {
                return $"{t/60.0/60.0:##.0} hours";
            }
            else if (t > 60)
            {
                return $"{t/60.0:##.0} minutes";
            }
            else
            {
                return $"{t:##.0} seconds";
            }
        }

        /// <summary>
        /// Dispose progress bar
        /// </summary>
        public void Dispose()
        {
        }

        #endregion Methods
    }
}
