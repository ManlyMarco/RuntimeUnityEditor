using System.Collections.Generic;

namespace RuntimeUnityEditor.Core.Utils
{
    internal class MovingAverage
    {
        private readonly int _windowSize;
        private readonly Queue<long> _samples;
        private long _sampleAccumulator;

        public MovingAverage(int windowSize = 11)
        {
            _windowSize = windowSize;
            _samples = new Queue<long>(_windowSize + 1);
        }

        ///// <summary>
        ///// Highest sample value ever, even if the sample is no longer counted in the average.
        ///// </summary>
        //public long PeakValue { get; private set; }

        public long GetAverage()
        {
            if (_samples.Count == 0)
                return 0;

            return _sampleAccumulator / _samples.Count;
        }

        public void Sample(long newSample)
        {
            _sampleAccumulator += newSample;
            _samples.Enqueue(newSample);

            if (_samples.Count > _windowSize)
                _sampleAccumulator -= _samples.Dequeue();

            //if (PeakValue < newSample)
            //    PeakValue = newSample;
        }
    }
}