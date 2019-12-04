using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Moments.Encoder;

namespace Gif3
{
    public class ThreadWorker
    {
        static int workerId = 1;

//        Thread m_Thread;

        internal Queue<GifFrame> m_Frames;
        internal GifEncoder m_Encoder;
        internal string m_FilePath;
        internal Action<string> m_OnFileSaved;
        internal Action<float> m_OnFileSaveProgress;
        internal int mCurrentEncodeFrame, m_MaxFrameCount;

        internal ThreadWorker(ThreadPriority priority)
        {
//            m_Thread = new Thread(Run);
//            m_Thread.Priority = priority;
        }

        internal void Start()
        {
//            m_Thread.Start();

            ThreadPool.QueueUserWorkItem(Run);
        }

        void Run(object state)
        {
            m_Encoder.Start(m_FilePath);
            Stopwatch sw = Stopwatch.StartNew();

            while (mCurrentEncodeFrame < m_MaxFrameCount || m_MaxFrameCount == 0)
            {
                while (m_Frames.Count > 0)
                {
//                    Debug.LogWarning(Time.realtimeSinceStartup - StartTime);
                    mCurrentEncodeFrame++;
                    m_Encoder.AddFrame(m_Frames.Dequeue());
//                    Debug.LogWarning(Time.realtimeSinceStartup - StartTime);
                    if (m_OnFileSaveProgress != null)
                    {
                        float percent = (float) mCurrentEncodeFrame / (float) m_MaxFrameCount;
                        m_OnFileSaveProgress(percent);
                    }
                }
            }


            m_Encoder.Finish();

            if (m_OnFileSaved != null)
                m_OnFileSaved(sw.ElapsedMilliseconds/1000f + "");
        }
    }
}