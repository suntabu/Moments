using System.Collections;
using Moments.Encoder;

namespace Gif2
{
    public class CoroutineWorker
    {
        internal GifEncoder m_Encoder;
        internal string m_FilePath;

        public CoroutineWorker(GifEncoder encoder, string filepath)
        {
            m_Encoder = encoder;
            m_FilePath = filepath;
        }

        public void Start()
        {
            m_Encoder.Start(m_FilePath);
        }

        public IEnumerator AddFrame(GifFrame frame)
        {
            yield return m_Encoder.CoAddFrame(frame);
        }

        public void Finish()
        {
            m_Encoder.Finish();
        }
    }
}