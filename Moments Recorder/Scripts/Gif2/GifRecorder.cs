using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Moments.Encoder;
using UnityObject = UnityEngine.Object;
using Min = Moments.MinAttribute;
namespace Gif2
{
    [AddComponentMenu("Miscellaneous/Moments Gif Recorder")]
    [RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    public sealed class GifRecorder : MonoBehaviour
    {
        public enum RecorderState
        {
            Recording,
            Recorded,
            None,
        }


        #region Exposed fields

        // These fields aren't public, the user shouldn't modify them directly as they can't break
        // everything if not used correctly. Use Setup() instead.

        [SerializeField, Moments.Min(8)] int m_Width = 320;

        [SerializeField, Moments.Min(8)] int m_Height = 200;

        [SerializeField] bool m_AutoAspect = true;

        [SerializeField, Range(1, 30)] int m_FramePerSecond = 15;

        [SerializeField, Moments.Min(-1)] int m_Repeat = 0;

        [SerializeField, Range(1, 100)] int m_Quality = 15;

        [SerializeField, Moments.Min(0.1f)] float m_BufferSize = 3f;

        #endregion

        #region Public fields

        private RecorderState mState = RecorderState.None;
        private Coroutine mCurrent;

        /// <summary>
        /// Current state of the recorder.
        /// </summary>
        public RecorderState State
        {
            get { return mState; }
            private set
            {
                if (mState != RecorderState.Recording && value == RecorderState.Recording)
                {
                    Worker.Start();

                    mCurrent = StartCoroutine(AddFrames());
                }

                if (mState == RecorderState.Recording && value == RecorderState.Recorded)
                {
                    Debug.LogError(Time.realtimeSinceStartup - StartTime);
                }

                mState = value;
            }
        }

        IEnumerator AddFrames()
        {
            while (mCurrentEncodeFrame <= m_MaxFrameCount)
            {
                while (m_Frames.Count > 0)
                {
//                    Debug.LogWarning(Time.realtimeSinceStartup - StartTime);
                    mCurrentEncodeFrame++;
                    yield return Worker.AddFrame(m_Frames.Dequeue());
//                    Debug.LogWarning(Time.realtimeSinceStartup - StartTime);
                }

                yield return null;
            }

            Debug.LogError(Time.realtimeSinceStartup - StartTime);

            Worker.Finish();

            if (OnFileSaved != null)
            {
                OnFileSaved(Worker.m_FilePath);
            }
        }


        internal string filename;

        private CoroutineWorker mWorker;

        internal CoroutineWorker Worker
        {
            get
            {
                if (mWorker == null)
                {
                    // Setup a worker thread and let it do its magic
                    GifEncoder encoder = new GifEncoder(m_Repeat, m_Quality);
                    encoder.SetDelay(Mathf.RoundToInt(m_TimePerFrame * 1000f));

                    if (string.IsNullOrEmpty(filename))
                        filename = GenerateFileName();
                    string filepath = SaveFolder + "/" + filename + ".gif";
                    mWorker = new CoroutineWorker(encoder, filepath);
                }
                return mWorker;
            }
        }


        /// <summary>
        /// The folder to save the gif to. No trailing slash.
        /// </summary>
        public string SaveFolder { get; set; }


        /// <summary>
        /// Returns the estimated VRam used (in MB) for recording.
        /// </summary>
        public float EstimatedMemoryUse
        {
            get
            {
                float mem = m_FramePerSecond * m_BufferSize;
                mem *= m_Width * m_Height * 4;
                mem /= 1024 * 1024;
                return mem;
            }
        }

        #endregion

        #region Delegates

        /// <summary>
        /// Called once a gif file has been saved. The first parameter will hold the worker ID and
        /// the second one the absolute file path.
        /// </summary>
        public Action<string> OnFileSaved;

        #endregion

        #region Internal fields

        int m_MaxFrameCount;
        float m_Time;
        float m_TimePerFrame;
        Queue<GifFrame> m_Frames;
        Moments.ReflectionUtils<GifRecorder> m_ReflectionUtils;
        private RenderTexture m_TempRt;
        private Texture2D m_TempTex;
        private int mCurrentRecordFrame, mCurrentEncodeFrame;

        #endregion

        #region Public API

        /// <summary>
        /// Initializes the component. Use this if you need to change the recorder settings in a script.
        /// This will flush the previously saved frames as settings can't be changed while recording.
        /// </summary>
        /// <param name="autoAspect">Automatically compute height from the current aspect ratio</param>
        /// <param name="width">Width in pixels</param>
        /// <param name="height">Height in pixels</param>
        /// <param name="fps">Recording FPS</param>
        /// <param name="bufferSize">Maximum amount of seconds to record to memory</param>
        /// <param name="repeat">-1: no repeat, 0: infinite, >0: repeat count</param>
        /// <param name="quality">Quality of color quantization (conversion of images to the maximum
        /// 256 colors allowed by the GIF specification). Lower values (minimum = 1) produce better
        /// colors, but slow processing significantly. Higher values will speed up the quantization
        /// pass at the cost of lower image quality (maximum = 100).</param>
        public void Setup(bool autoAspect, int width, int height, int fps, float bufferSize, int repeat,
            int quality)
        {
            if (State == RecorderState.Recording)
            {
                Debug.LogWarning("Attempting to setup the component during the pre-processing step.");
                return;
            }

            // Start fresh
            FlushMemory();

            // Set values and validate them
            m_AutoAspect = autoAspect;
            m_ReflectionUtils.ConstrainMin(x => x.m_Width, width);

            if (!autoAspect)
                m_ReflectionUtils.ConstrainMin(x => x.m_Height, height);

            m_ReflectionUtils.ConstrainRange(x => x.m_FramePerSecond, fps);
            m_ReflectionUtils.ConstrainMin(x => x.m_BufferSize, bufferSize);
            m_ReflectionUtils.ConstrainMin(x => x.m_Repeat, repeat);
            m_ReflectionUtils.ConstrainRange(x => x.m_Quality, quality);

            // Ready to go
            Init();
        }


        private float StartTime = 0;

        /// <summary>
        /// Starts or resumes recording. You can't resume while it's pre-processing data to be saved.
        /// </summary>
        public void Record()
        {
            StartTime = Time.realtimeSinceStartup;
            if (State == RecorderState.Recording)
            {
                Debug.LogWarning("Attempting to resume recording during the pre-processing step.");
                return;
            }

            State = RecorderState.Recording;
        }

        /// <summary>
        /// Clears all saved frames from memory and starts fresh.
        /// </summary>
        public void FlushMemory()
        {
            if (State == RecorderState.None)
            {
                Debug.LogWarning("Attempting to flush memory during the pre-processing step.");
                return;
            }

            Init();


            if (m_Frames == null)
                return;

            Flush(m_TempRt);
            Flush(m_TempTex);

            m_TempRt = null;
            m_TempTex = null;

            m_Frames.Clear();
        }

        #endregion

        #region Unity events

        void Awake()
        {
            m_ReflectionUtils = new Moments.ReflectionUtils<GifRecorder>(this);
            m_Frames = new Queue<GifFrame>();
            Init();
        }

        void OnDestroy()
        {
            FlushMemory();
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (State != RecorderState.Recording)
            {
                Graphics.Blit(source, destination);
                return;
            }

            m_Time += Time.unscaledDeltaTime;

            if (m_Time >= m_TimePerFrame)
            {
                m_Time -= m_TimePerFrame;

                if (m_TempRt == null)
                {
                    m_TempRt = new RenderTexture(m_Width, m_Height, 0, RenderTextureFormat.ARGB32)
                    {
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        anisoLevel = 0
                    };
                }


                Graphics.Blit(source, m_TempRt);

                RenderTexture.active = null;
                GifFrame frame = ToGifFrame(m_TempRt, TempTex);
                m_Frames.Enqueue(frame);

                mCurrentRecordFrame++;
                if (mCurrentRecordFrame > m_MaxFrameCount)
                {
                    State = RecorderState.Recorded;
                }
            }
            else

                Graphics.Blit(source, destination);
        }

        #endregion

        #region Methods

        // Used to reset internal values, called on Start(), Setup() and FlushMemory()
        void Init()
        {
            State = RecorderState.None;
            ComputeHeight();
            m_MaxFrameCount = Mathf.RoundToInt(m_BufferSize * m_FramePerSecond);
            m_TimePerFrame = 1f / m_FramePerSecond;
            m_Time = 0f;

            // Make sure the output folder is set or use the default one
            if (string.IsNullOrEmpty(SaveFolder))
            {
#if UNITY_EDITOR
                SaveFolder =
                    Application
                        .persistentDataPath; // Defaults to the asset folder in the editor for faster access to the gif file
#else
				SaveFolder = Application.persistentDataPath;
#endif
            }
        }

        // Automatically computes height from the current aspect ratio if auto aspect is set to true
        public void ComputeHeight()
        {
            if (!m_AutoAspect)
                return;

            m_Height = Mathf.RoundToInt(m_Width / GetComponent<Camera>().aspect);
        }

        void Flush(UnityObject obj)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
#else
            UnityObject.Destroy(obj);
#endif
        }

        // Gets a filename : GifCapture-yyyyMMddHHmmssffff
        string GenerateFileName()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssffff");
            return "GifCapture-" + timestamp;
        }


        /// <summary>
        /// Get a temporary texture to read RenderTexture data
        /// </summary>
        private Texture2D TempTex
        {
            get
            {
                if (m_TempTex == null)
                {
                    m_TempTex = new Texture2D(m_Width, m_Height, TextureFormat.RGB24, false)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        anisoLevel = 0
                    };
                }

                return m_TempTex;
            }
        }


        // Converts a RenderTexture to a GifFrame
        // Should be fast enough for low-res textures but will tank the framerate at higher res
        GifFrame ToGifFrame(RenderTexture source, Texture2D target)
        {
            RenderTexture.active = source;
            target.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
//            target.Apply();
            RenderTexture.active = null;

            return new GifFrame() {Width = target.width, Height = target.height, Data = target.GetPixels32()};
        }

        #endregion
    }
}