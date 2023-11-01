using Godot;
using System;
using System.Collections.Generic;

// /////////////////////////////////////////////////////////////////////////////////////////
//
// Ported from More Effective Coroutines (Free) v3.10.2 by WeaverDev
// https://assetstore.unity.com/packages/tools/animation/more-effective-coroutines-free-54975
// Published with permission under the MIT license
//
// Originally Created by Teal Rogers
// Trinary Software
// http://trinary.tech/
//
// /////////////////////////////////////////////////////////////////////////////////////////

namespace MEC
{
    public partial class Timing : Node
    {
        /// <summary>
        /// The number of coroutines that are being run in the Process segment.
        /// Note, this is updated every <variable>FramesUntilMaintenance</variable> frames.
        /// </summary>
        [Export]
        public int ProcessCoroutines;
        /// <summary>
        /// The number of coroutines that are being run in the PhysicsProcess segment.
        /// Note, this is updated every <variable>FramesUntilMaintenance</variable> frames.
        /// </summary>
        [Export]
        public int PhysicsProcessCoroutines;
        /// <summary>
        /// The number of coroutines that are being run in the DeferredProcess segment.
        /// Note, this is updated every <variable>FramesUntilMaintenance</variable> frames.
        /// </summary>
        [Export]
        public int DeferredProcessCoroutines;
        /// <summary>
        /// The time in seconds that the current segment has been running.
        /// </summary>
        [System.NonSerialized]
        public double localTime;
        /// <summary>
        /// The amount of time in fractional seconds that elapsed between this frame and the last frame.
        /// </summary>
        [System.NonSerialized]
        public double deltaTime;
        /// <summary>
        /// The amount of time in fractional seconds that elapsed between this frame and the last frame.
        /// </summary>
        public static double DeltaTime { get { return Instance.deltaTime; } }
        /// <summary>
        /// Used for advanced coroutine control.
        /// </summary>
        public static System.Func<IEnumerator<double>, CoroutineHandle, IEnumerator<double>> ReplacementFunction;
        /// <summary>
        /// You can use "yield return Timing.WaitForOneFrame;" inside a coroutine function to go to the next frame. 
        /// </summary>
        public const double WaitForOneFrame = double.NegativeInfinity;
        /// <summary>
        /// The main thread that (almost) everything in godot runs in.
        /// </summary>
        public static System.Threading.Thread MainThread { get; private set; }
        /// <summary>
        /// The handle of the current coroutine that is running.
        /// </summary>
        public CoroutineHandle currentCoroutine { get; private set; }

        private static object _tmpRef;

        private ulong _currentProcessFrame;
        private ulong _currentDeferredProcessFrame;
        private int _nextProcessProcessSlot;
        private int _nextDeferredProcessProcessSlot;
        private int _nextPhysicsProcessProcessSlot;
        private int _lastProcessProcessSlot;
        private int _lastDeferredProcessProcessSlot;
        private int _lastPhysicsProcessProcessSlot;
        private double _lastProcessTime;
        private double _lastDeferredProcessTime;
        private double _physicsProcessTime;
        private double _lastPhysicsProcessTime;
        private ushort _framesSinceProcess;
        private ushort _expansions = 1;
        private byte _instanceID;

        private readonly Dictionary<CoroutineHandle, HashSet<CoroutineHandle>> _waitingTriggers = new Dictionary<CoroutineHandle, HashSet<CoroutineHandle>>();
        private readonly HashSet<CoroutineHandle> _allWaiting = new HashSet<CoroutineHandle>();
        private readonly Dictionary<CoroutineHandle, ProcessIndex> _handleToIndex = new Dictionary<CoroutineHandle, ProcessIndex>();
        private readonly Dictionary<ProcessIndex, CoroutineHandle> _indexToHandle = new Dictionary<ProcessIndex, CoroutineHandle>();
        private readonly Dictionary<CoroutineHandle, string> _processTags = new Dictionary<CoroutineHandle, string>();
        private readonly Dictionary<string, HashSet<CoroutineHandle>> _taggedProcesses = new Dictionary<string, HashSet<CoroutineHandle>>();

        private IEnumerator<double>[] ProcessProcesses = new IEnumerator<double>[InitialBufferSizeLarge];
        private IEnumerator<double>[] DeferredProcessProcesses = new IEnumerator<double>[InitialBufferSizeSmall];
        private IEnumerator<double>[] PhysicsProcessProcesses = new IEnumerator<double>[InitialBufferSizeMedium];

        private bool[] ProcessPaused = new bool[InitialBufferSizeLarge];
        private bool[] DeferredProcessPaused = new bool[InitialBufferSizeSmall];
        private bool[] PhysicsProcessPaused = new bool[InitialBufferSizeMedium];
        private bool[] ProcessHeld = new bool[InitialBufferSizeLarge];
        private bool[] DeferredProcessHeld = new bool[InitialBufferSizeSmall];
        private bool[] PhysicsProcessHeld = new bool[InitialBufferSizeMedium];

        private const ushort FramesUntilMaintenance = 64;
        private const int ProcessArrayChunkSize = 64;
        private const int InitialBufferSizeLarge = 256;
        private const int InitialBufferSizeMedium = 64;
        private const int InitialBufferSizeSmall = 8;

        private static Timing[] ActiveInstances = new Timing[16];
        private static Timing _instance;

        public Timing()
        {
            InitializeInstanceID();
        }

        public static Timing Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Check if we were loaded via Autoload
                    _instance = ((SceneTree)Engine.GetMainLoop()).Root.GetNodeOrNull<Timing>(typeof(Timing).Name);
                    if (_instance == null)
                    {
                        // Instantiate to root at runtime
                        _instance = new Timing();
                        _instance.Name = typeof(Timing).Name;
                        _instance.CallDeferred(nameof(InitGlobalInstance));
                    }
                }
                return _instance;
            }
        }

        private void InitGlobalInstance()
        {
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(this);
        }

        public override void _Ready()
        {
            // We process before other nodes by default
            ProcessPriority = -1;

            // Godot 4.1 only, 4.0 does not implement this.
            // Use reflection to try and set it for compatibility.
            //ProcessPhysicsPriority = -1;
            try
            {
                GetType().GetProperty("ProcessPhysicsPriority",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public).SetValue(this, -1);
            }
            catch (NullReferenceException) { }

            if (MainThread == null)
                MainThread = System.Threading.Thread.CurrentThread;
        }

        public override void _ExitTree()
        {
            if (_instanceID < ActiveInstances.Length)
                ActiveInstances[_instanceID] = null;
        }

        private void InitializeInstanceID()
        {
            if (ActiveInstances[_instanceID] == null)
            {
                if (_instanceID == 0x00)
                    _instanceID++;

                for (; _instanceID <= 0x10; _instanceID++)
                {
                    if (_instanceID == 0x10)
                    {
                        QueueFree();
                        throw new System.OverflowException("You are only allowed 15 different contexts for MEC to run inside at one time.");
                    }

                    if (ActiveInstances[_instanceID] == null)
                    {
                        ActiveInstances[_instanceID] = this;
                        break;
                    }
                }
            }
        }

        public override void _Process(double delta)
        {
            if (_nextProcessProcessSlot > 0)
            {
                ProcessIndex coindex = new ProcessIndex { seg = Segment.Process };
                if (UpdateTimeValues(coindex.seg))
                    _lastProcessProcessSlot = _nextProcessProcessSlot;

                for (coindex.i = 0; coindex.i < _lastProcessProcessSlot; coindex.i++)
                {
                    try
                    {
                        if (!ProcessPaused[coindex.i] && !ProcessHeld[coindex.i] && ProcessProcesses[coindex.i] != null && !(localTime < ProcessProcesses[coindex.i].Current))
                        {
                            currentCoroutine = _indexToHandle[coindex];

                            if (!ProcessProcesses[coindex.i].MoveNext())
                            {
                                if (_indexToHandle.ContainsKey(coindex))
                                    KillCoroutinesOnInstance(_indexToHandle[coindex]);
                            }
                            else if (ProcessProcesses[coindex.i] != null && double.IsNaN(ProcessProcesses[coindex.i].Current))
                            {
                                if (ReplacementFunction != null)
                                {
                                    ProcessProcesses[coindex.i] = ReplacementFunction(ProcessProcesses[coindex.i], _indexToHandle[coindex]);
                                    ReplacementFunction = null;
                                }
                                coindex.i--;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        GD.PrintErr(ex);
                    }
                }
            }

            currentCoroutine = default(CoroutineHandle);

            if (++_framesSinceProcess > FramesUntilMaintenance)
            {
                _framesSinceProcess = 0;

                RemoveUnused();
            }

            CallDeferred(nameof(_DeferredProcess));
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_nextPhysicsProcessProcessSlot > 0)
            {
                ProcessIndex coindex = new ProcessIndex { seg = Segment.PhysicsProcess };
                if (UpdateTimeValues(coindex.seg))
                    _lastPhysicsProcessProcessSlot = _nextPhysicsProcessProcessSlot;

                for (coindex.i = 0; coindex.i < _lastPhysicsProcessProcessSlot; coindex.i++)
                {
                    try
                    {
                        if (!PhysicsProcessPaused[coindex.i] && !PhysicsProcessHeld[coindex.i] && PhysicsProcessProcesses[coindex.i] != null && !(localTime < PhysicsProcessProcesses[coindex.i].Current))
                        {
                            currentCoroutine = _indexToHandle[coindex];

                            if (!PhysicsProcessProcesses[coindex.i].MoveNext())
                            {
                                if (_indexToHandle.ContainsKey(coindex))
                                    KillCoroutinesOnInstance(_indexToHandle[coindex]);
                            }
                            else if (PhysicsProcessProcesses[coindex.i] != null && double.IsNaN(PhysicsProcessProcesses[coindex.i].Current))
                            {
                                if (ReplacementFunction != null)
                                {
                                    PhysicsProcessProcesses[coindex.i] = ReplacementFunction(PhysicsProcessProcesses[coindex.i], _indexToHandle[coindex]);
                                    ReplacementFunction = null;
                                }
                                coindex.i--;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        GD.PrintErr(ex);
                    }
                }

                currentCoroutine = default;
            }
        }

        private void _DeferredProcess()
        {
            if (_nextDeferredProcessProcessSlot > 0)
            {
                ProcessIndex coindex = new ProcessIndex { seg = Segment.DeferredProcess };
                if (UpdateTimeValues(coindex.seg))
                    _lastDeferredProcessProcessSlot = _nextDeferredProcessProcessSlot;

                for (coindex.i = 0; coindex.i < _lastDeferredProcessProcessSlot; coindex.i++)
                {
                    try
                    {
                        if (!DeferredProcessPaused[coindex.i] && !DeferredProcessHeld[coindex.i] && DeferredProcessProcesses[coindex.i] != null && !(localTime < DeferredProcessProcesses[coindex.i].Current))
                        {
                            currentCoroutine = _indexToHandle[coindex];

                            if (!DeferredProcessProcesses[coindex.i].MoveNext())
                            {
                                if (_indexToHandle.ContainsKey(coindex))
                                    KillCoroutinesOnInstance(_indexToHandle[coindex]);
                            }
                            else if (DeferredProcessProcesses[coindex.i] != null && double.IsNaN(DeferredProcessProcesses[coindex.i].Current))
                            {
                                if (ReplacementFunction != null)
                                {
                                    DeferredProcessProcesses[coindex.i] = ReplacementFunction(DeferredProcessProcesses[coindex.i], _indexToHandle[coindex]);
                                    ReplacementFunction = null;
                                }
                                coindex.i--;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        GD.PrintErr(ex);
                    }
                }
                currentCoroutine = default(CoroutineHandle);
            }
        }

        private void RemoveUnused()
        {
            var waitTrigsEnum = _waitingTriggers.GetEnumerator();
            while (waitTrigsEnum.MoveNext())
            {
                if (waitTrigsEnum.Current.Value.Count == 0)
                {
                    _waitingTriggers.Remove(waitTrigsEnum.Current.Key);
                    waitTrigsEnum = _waitingTriggers.GetEnumerator();
                    continue;
                }

                if (_handleToIndex.ContainsKey(waitTrigsEnum.Current.Key) && CoindexIsNull(_handleToIndex[waitTrigsEnum.Current.Key]))
                {
                    CloseWaitingProcess(waitTrigsEnum.Current.Key);
                    waitTrigsEnum = _waitingTriggers.GetEnumerator();
                }
            }

            ProcessIndex outer, inner;
            outer.seg = inner.seg = Segment.Process;

            for (outer.i = inner.i = 0; outer.i < _nextProcessProcessSlot; outer.i++)
            {
                if (ProcessProcesses[outer.i] != null)
                {
                    if (outer.i != inner.i)
                    {
                        ProcessProcesses[inner.i] = ProcessProcesses[outer.i];
                        ProcessPaused[inner.i] = ProcessPaused[outer.i];
                        ProcessHeld[inner.i] = ProcessHeld[outer.i];

                        if (_indexToHandle.ContainsKey(inner))
                        {
                            RemoveTag(_indexToHandle[inner]);
                            _handleToIndex.Remove(_indexToHandle[inner]);
                            _indexToHandle.Remove(inner);
                        }

                        _handleToIndex[_indexToHandle[outer]] = inner;
                        _indexToHandle.Add(inner, _indexToHandle[outer]);
                        _indexToHandle.Remove(outer);
                    }
                    inner.i++;
                }
            }
            for (outer.i = inner.i; outer.i < _nextProcessProcessSlot; outer.i++)
            {
                ProcessProcesses[outer.i] = null;
                ProcessPaused[outer.i] = false;
                ProcessHeld[outer.i] = false;

                if (_indexToHandle.ContainsKey(outer))
                {
                    RemoveTag(_indexToHandle[outer]);

                    _handleToIndex.Remove(_indexToHandle[outer]);
                    _indexToHandle.Remove(outer);
                }
            }

            _lastProcessProcessSlot -= _nextProcessProcessSlot - inner.i;
            ProcessCoroutines = _nextProcessProcessSlot = inner.i;

            outer.seg = inner.seg = Segment.PhysicsProcess;
            for (outer.i = inner.i = 0; outer.i < _nextPhysicsProcessProcessSlot; outer.i++)
            {
                if (PhysicsProcessProcesses[outer.i] != null)
                {
                    if (outer.i != inner.i)
                    {
                        PhysicsProcessProcesses[inner.i] = PhysicsProcessProcesses[outer.i];
                        PhysicsProcessPaused[inner.i] = PhysicsProcessPaused[outer.i];
                        PhysicsProcessHeld[inner.i] = PhysicsProcessHeld[outer.i];

                        if (_indexToHandle.ContainsKey(inner))
                        {
                            RemoveTag(_indexToHandle[inner]);
                            _handleToIndex.Remove(_indexToHandle[inner]);
                            _indexToHandle.Remove(inner);
                        }

                        _handleToIndex[_indexToHandle[outer]] = inner;
                        _indexToHandle.Add(inner, _indexToHandle[outer]);
                        _indexToHandle.Remove(outer);
                    }
                    inner.i++;
                }
            }
            for (outer.i = inner.i; outer.i < _nextPhysicsProcessProcessSlot; outer.i++)
            {
                PhysicsProcessProcesses[outer.i] = null;
                PhysicsProcessPaused[outer.i] = false;
                PhysicsProcessHeld[outer.i] = false;

                if (_indexToHandle.ContainsKey(outer))
                {
                    RemoveTag(_indexToHandle[outer]);

                    _handleToIndex.Remove(_indexToHandle[outer]);
                    _indexToHandle.Remove(outer);
                }
            }

            _lastPhysicsProcessProcessSlot -= _nextPhysicsProcessProcessSlot - inner.i;
            PhysicsProcessCoroutines = _nextPhysicsProcessProcessSlot = inner.i;

            outer.seg = inner.seg = Segment.DeferredProcess;
            for (outer.i = inner.i = 0; outer.i < _nextDeferredProcessProcessSlot; outer.i++)
            {
                if (DeferredProcessProcesses[outer.i] != null)
                {
                    if (outer.i != inner.i)
                    {
                        DeferredProcessProcesses[inner.i] = DeferredProcessProcesses[outer.i];
                        DeferredProcessPaused[inner.i] = DeferredProcessPaused[outer.i];
                        DeferredProcessHeld[inner.i] = DeferredProcessHeld[outer.i];

                        if (_indexToHandle.ContainsKey(inner))
                        {
                            RemoveTag(_indexToHandle[inner]);
                            _handleToIndex.Remove(_indexToHandle[inner]);
                            _indexToHandle.Remove(inner);
                        }

                        _handleToIndex[_indexToHandle[outer]] = inner;
                        _indexToHandle.Add(inner, _indexToHandle[outer]);
                        _indexToHandle.Remove(outer);
                    }
                    inner.i++;
                }
            }
            for (outer.i = inner.i; outer.i < _nextDeferredProcessProcessSlot; outer.i++)
            {
                DeferredProcessProcesses[outer.i] = null;
                DeferredProcessPaused[outer.i] = false;
                DeferredProcessHeld[outer.i] = false;

                if (_indexToHandle.ContainsKey(outer))
                {
                    RemoveTag(_indexToHandle[outer]);

                    _handleToIndex.Remove(_indexToHandle[outer]);
                    _indexToHandle.Remove(outer);
                }
            }

            _lastDeferredProcessProcessSlot -= _nextDeferredProcessProcessSlot - inner.i;
            DeferredProcessCoroutines = _nextDeferredProcessProcessSlot = inner.i;
        }

        /// <summary>
        /// Run a new coroutine in the Process segment.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public static CoroutineHandle RunCoroutine(IEnumerator<double> coroutine)
        {
            return coroutine == null ? new CoroutineHandle()
                : Instance.RunCoroutineInternal(coroutine, Segment.Process, null, new CoroutineHandle(Instance._instanceID), true);
        }

        /// <summary>
        /// Run a new coroutine in the Process segment.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <param name="tag">An optional tag to attach to the coroutine which can later be used for Kill operations.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public static CoroutineHandle RunCoroutine(IEnumerator<double> coroutine, string tag)
        {
            return coroutine == null ? new CoroutineHandle()
                : Instance.RunCoroutineInternal(coroutine, Segment.Process, tag, new CoroutineHandle(Instance._instanceID), true);
        }

        /// <summary>
        /// Run a new coroutine.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <param name="segment">The segment that the coroutine should run in.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public static CoroutineHandle RunCoroutine(IEnumerator<double> coroutine, Segment segment)
        {
            return coroutine == null ? new CoroutineHandle()
                : Instance.RunCoroutineInternal(coroutine, segment, null, new CoroutineHandle(Instance._instanceID), true);
        }

        /// <summary>
        /// Run a new coroutine.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <param name="segment">The segment that the coroutine should run in.</param>
        /// <param name="tag">An optional tag to attach to the coroutine which can later be used for Kill operations.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public static CoroutineHandle RunCoroutine(IEnumerator<double> coroutine, Segment segment, string tag)
        {
            return coroutine == null ? new CoroutineHandle()
                : Instance.RunCoroutineInternal(coroutine, segment, tag, new CoroutineHandle(Instance._instanceID), true);
        }

        /// <summary>
        /// Run a new coroutine on this Timing instance in the Process segment.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public CoroutineHandle RunCoroutineOnInstance(IEnumerator<double> coroutine)
        {
            return coroutine == null ? new CoroutineHandle()
                 : RunCoroutineInternal(coroutine, Segment.Process, null, new CoroutineHandle(_instanceID), true);
        }

        /// <summary>
        /// Run a new coroutine on this Timing instance in the Process segment.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <param name="tag">An optional tag to attach to the coroutine which can later be used for Kill operations.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public CoroutineHandle RunCoroutineOnInstance(IEnumerator<double> coroutine, string tag)
        {
            return coroutine == null ? new CoroutineHandle()
                 : RunCoroutineInternal(coroutine, Segment.Process, tag, new CoroutineHandle(_instanceID), true);
        }

        /// <summary>
        /// Run a new coroutine on this Timing instance.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <param name="segment">The segment that the coroutine should run in.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public CoroutineHandle RunCoroutineOnInstance(IEnumerator<double> coroutine, Segment segment)
        {
            return coroutine == null ? new CoroutineHandle()
                 : RunCoroutineInternal(coroutine, segment, null, new CoroutineHandle(_instanceID), true);
        }

        /// <summary>
        /// Run a new coroutine on this Timing instance.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <param name="segment">The segment that the coroutine should run in.</param>
        /// <param name="tag">An optional tag to attach to the coroutine which can later be used for Kill operations.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public CoroutineHandle RunCoroutineOnInstance(IEnumerator<double> coroutine, Segment segment, string tag)
        {
            return coroutine == null ? new CoroutineHandle()
                 : RunCoroutineInternal(coroutine, segment, tag, new CoroutineHandle(_instanceID), true);
        }


        private CoroutineHandle RunCoroutineInternal(IEnumerator<double> coroutine, Segment segment, string tag, CoroutineHandle handle, bool prewarm)
        {
            ProcessIndex slot = new ProcessIndex { seg = segment };

            if (_handleToIndex.ContainsKey(handle))
            {
                _indexToHandle.Remove(_handleToIndex[handle]);
                _handleToIndex.Remove(handle);
            }

            double currentLocalTime = localTime;
            double currentDeltaTime = deltaTime;
            CoroutineHandle cachedHandle = currentCoroutine;
            currentCoroutine = handle;

            switch (segment)
            {
                case Segment.Process:

                    if (_nextProcessProcessSlot >= ProcessProcesses.Length)
                    {
                        IEnumerator<double>[] oldProcArray = ProcessProcesses;
                        bool[] oldPausedArray = ProcessPaused;
                        bool[] oldHeldArray = ProcessHeld;

                        ProcessProcesses = new IEnumerator<double>[ProcessProcesses.Length + (ProcessArrayChunkSize * _expansions++)];
                        ProcessPaused = new bool[ProcessProcesses.Length];
                        ProcessHeld = new bool[ProcessProcesses.Length];

                        for (int i = 0; i < oldProcArray.Length; i++)
                        {
                            ProcessProcesses[i] = oldProcArray[i];
                            ProcessPaused[i] = oldPausedArray[i];
                            ProcessHeld[i] = oldHeldArray[i];
                        }
                    }

                    if (UpdateTimeValues(slot.seg))
                        _lastProcessProcessSlot = _nextProcessProcessSlot;

                    slot.i = _nextProcessProcessSlot++;
                    ProcessProcesses[slot.i] = coroutine;

                    if (null != tag)
                        AddTag(tag, handle);

                    _indexToHandle.Add(slot, handle);
                    _handleToIndex.Add(handle, slot);

                    while (prewarm)
                    {
                        if (!ProcessProcesses[slot.i].MoveNext())
                        {
                            if (_indexToHandle.ContainsKey(slot))
                                KillCoroutinesOnInstance(_indexToHandle[slot]);

                            prewarm = false;
                        }
                        else if (ProcessProcesses[slot.i] != null && double.IsNaN(ProcessProcesses[slot.i].Current))
                        {
                            if (ReplacementFunction != null)
                            {
                                ProcessProcesses[slot.i] = ReplacementFunction(ProcessProcesses[slot.i], _indexToHandle[slot]);
                                ReplacementFunction = null;
                            }
                            prewarm = !ProcessPaused[slot.i] && !ProcessHeld[slot.i];
                        }
                        else
                        {
                            prewarm = false;
                        }
                    }

                    break;

                case Segment.PhysicsProcess:

                    if (_nextPhysicsProcessProcessSlot >= PhysicsProcessProcesses.Length)
                    {
                        IEnumerator<double>[] oldProcArray = PhysicsProcessProcesses;
                        bool[] oldPausedArray = PhysicsProcessPaused;
                        bool[] oldHeldArray = PhysicsProcessHeld;

                        PhysicsProcessProcesses = new IEnumerator<double>[PhysicsProcessProcesses.Length + (ProcessArrayChunkSize * _expansions++)];
                        PhysicsProcessPaused = new bool[PhysicsProcessProcesses.Length];
                        PhysicsProcessHeld = new bool[PhysicsProcessProcesses.Length];

                        for (int i = 0; i < oldProcArray.Length; i++)
                        {
                            PhysicsProcessProcesses[i] = oldProcArray[i];
                            PhysicsProcessPaused[i] = oldPausedArray[i];
                            PhysicsProcessHeld[i] = oldHeldArray[i];
                        }
                    }

                    if (UpdateTimeValues(slot.seg))
                        _lastPhysicsProcessProcessSlot = _nextPhysicsProcessProcessSlot;

                    slot.i = _nextPhysicsProcessProcessSlot++;
                    PhysicsProcessProcesses[slot.i] = coroutine;

                    if (null != tag)
                        AddTag(tag, handle);

                    _indexToHandle.Add(slot, handle);
                    _handleToIndex.Add(handle, slot);

                    while (prewarm)
                    {
                        if (!PhysicsProcessProcesses[slot.i].MoveNext())
                        {
                            if (_indexToHandle.ContainsKey(slot))
                                KillCoroutinesOnInstance(_indexToHandle[slot]);

                            prewarm = false;
                        }
                        else if (PhysicsProcessProcesses[slot.i] != null && double.IsNaN(PhysicsProcessProcesses[slot.i].Current))
                        {
                            if (ReplacementFunction != null)
                            {
                                PhysicsProcessProcesses[slot.i] = ReplacementFunction(PhysicsProcessProcesses[slot.i], _indexToHandle[slot]);
                                ReplacementFunction = null;
                            }
                            prewarm = !PhysicsProcessPaused[slot.i] && !PhysicsProcessHeld[slot.i];
                        }
                        else
                        {
                            prewarm = false;
                        }
                    }

                    break;

                case Segment.DeferredProcess:

                    if (_nextDeferredProcessProcessSlot >= DeferredProcessProcesses.Length)
                    {
                        IEnumerator<double>[] oldProcArray = DeferredProcessProcesses;
                        bool[] oldPausedArray = DeferredProcessPaused;
                        bool[] oldHeldArray = DeferredProcessHeld;

                        DeferredProcessProcesses = new IEnumerator<double>[DeferredProcessProcesses.Length + (ProcessArrayChunkSize * _expansions++)];
                        DeferredProcessPaused = new bool[DeferredProcessProcesses.Length];
                        DeferredProcessHeld = new bool[DeferredProcessProcesses.Length];

                        for (int i = 0; i < oldProcArray.Length; i++)
                        {
                            DeferredProcessProcesses[i] = oldProcArray[i];
                            DeferredProcessPaused[i] = oldPausedArray[i];
                            DeferredProcessHeld[i] = oldHeldArray[i];
                        }
                    }

                    if (UpdateTimeValues(slot.seg))
                        _lastDeferredProcessProcessSlot = _nextDeferredProcessProcessSlot;

                    slot.i = _nextDeferredProcessProcessSlot++;
                    DeferredProcessProcesses[slot.i] = coroutine;

                    if (tag != null)
                        AddTag(tag, handle);

                    _indexToHandle.Add(slot, handle);
                    _handleToIndex.Add(handle, slot);

                    while (prewarm)
                    {
                        if (!DeferredProcessProcesses[slot.i].MoveNext())
                        {
                            if (_indexToHandle.ContainsKey(slot))
                                KillCoroutinesOnInstance(_indexToHandle[slot]);

                            prewarm = false;
                        }
                        else if (DeferredProcessProcesses[slot.i] != null && double.IsNaN(DeferredProcessProcesses[slot.i].Current))
                        {
                            if (ReplacementFunction != null)
                            {
                                DeferredProcessProcesses[slot.i] = ReplacementFunction(DeferredProcessProcesses[slot.i], _indexToHandle[slot]);
                                ReplacementFunction = null;
                            }
                            prewarm = !DeferredProcessPaused[slot.i] && !DeferredProcessHeld[slot.i];
                        }
                        else
                        {
                            prewarm = false;
                        }
                    }

                    break;

                default:
                    handle = new CoroutineHandle();
                    break;
            }

            localTime = currentLocalTime;
            deltaTime = currentDeltaTime;
            currentCoroutine = cachedHandle;

            return handle;
        }

        /// <summary>
        /// This will kill all coroutines running on the main MEC instance and reset the context.
        /// NOTE: If you call this function from within a running coroutine then you MUST end the current
        /// coroutine. If the running coroutine has more work to do you may run a new "part 2" coroutine 
        /// function to complete the task before ending the current one.
        /// </summary>
        /// <returns>The number of coroutines that were killed.</returns>
        public static int KillCoroutines()
        {
            return _instance == null ? 0 : _instance.KillCoroutinesOnInstance();
        }

        /// <summary>
        /// This will kill all coroutines running on the current MEC instance and reset the context.
        /// NOTE: If you call this function from within a running coroutine then you MUST end the current
        /// coroutine. If the running coroutine has more work to do you may run a new "part 2" coroutine 
        /// function to complete the task before ending the current one.
        /// </summary>
        /// <returns>The number of coroutines that were killed.</returns>
        public int KillCoroutinesOnInstance()
        {
            int retVal = _nextProcessProcessSlot + _nextDeferredProcessProcessSlot + _nextPhysicsProcessProcessSlot;

            ProcessProcesses = new IEnumerator<double>[InitialBufferSizeLarge];
            ProcessPaused = new bool[InitialBufferSizeLarge];
            ProcessHeld = new bool[InitialBufferSizeLarge];
            ProcessCoroutines = 0;
            _nextProcessProcessSlot = 0;

            DeferredProcessProcesses = new IEnumerator<double>[InitialBufferSizeSmall];
            DeferredProcessPaused = new bool[InitialBufferSizeSmall];
            DeferredProcessHeld = new bool[InitialBufferSizeSmall];
            DeferredProcessCoroutines = 0;
            _nextDeferredProcessProcessSlot = 0;

            PhysicsProcessProcesses = new IEnumerator<double>[InitialBufferSizeMedium];
            PhysicsProcessPaused = new bool[InitialBufferSizeMedium];
            PhysicsProcessHeld = new bool[InitialBufferSizeMedium];
            PhysicsProcessCoroutines = 0;
            _nextPhysicsProcessProcessSlot = 0;

            _processTags.Clear();
            _taggedProcesses.Clear();
            _handleToIndex.Clear();
            _indexToHandle.Clear();
            _waitingTriggers.Clear();
            _expansions = (ushort)((_expansions / 2) + 1);

            return retVal;
        }

        /// <summary>
        /// Kills the instances of the coroutine handle if it exists.
        /// </summary>
        /// <param name="handle">The handle of the coroutine to kill.</param>
        /// <returns>The number of coroutines that were found and killed (0 or 1).</returns>
        public static int KillCoroutines(CoroutineHandle handle)
        {
            return ActiveInstances[handle.Key] != null ? GetInstance(handle.Key).KillCoroutinesOnInstance(handle) : 0;
        }

        /// <summary>
        /// Kills the instance of the coroutine handle on this Timing instance if it exists.
        /// </summary>
        /// <param name="handle">The handle of the coroutine to kill.</param>
        /// <returns>The number of coroutines that were found and killed (0 or 1).</returns>
        public int KillCoroutinesOnInstance(CoroutineHandle handle)
        {
            bool foundOne = false;

            if (_handleToIndex.ContainsKey(handle))
            {
                if (_waitingTriggers.ContainsKey(handle))
                    CloseWaitingProcess(handle);

                foundOne = CoindexExtract(_handleToIndex[handle]) != null;
                RemoveTag(handle);
            }

            return foundOne ? 1 : 0;
        }

        /// <summary>
        /// Kills all coroutines that have the given tag.
        /// </summary>
        /// <param name="tag">All coroutines with this tag will be killed.</param>
        /// <returns>The number of coroutines that were found and killed.</returns>
        public static int KillCoroutines(string tag)
        {
            return _instance == null ? 0 : _instance.KillCoroutinesOnInstance(tag);
        }

        /// <summary> 
        /// Kills all coroutines that have the given tag.
        /// </summary>
        /// <param name="tag">All coroutines with this tag will be killed.</param>
        /// <returns>The number of coroutines that were found and killed.</returns>
        public int KillCoroutinesOnInstance(string tag)
        {
            if (tag == null) return 0;
            int numberFound = 0;

            while (_taggedProcesses.ContainsKey(tag))
            {
                var matchEnum = _taggedProcesses[tag].GetEnumerator();
                matchEnum.MoveNext();

                if (Nullify(_handleToIndex[matchEnum.Current]))
                {
                    if (_waitingTriggers.ContainsKey(matchEnum.Current))
                        CloseWaitingProcess(matchEnum.Current);

                    numberFound++;
                }

                RemoveTag(matchEnum.Current);

                if (_handleToIndex.ContainsKey(matchEnum.Current))
                {
                    _indexToHandle.Remove(_handleToIndex[matchEnum.Current]);
                    _handleToIndex.Remove(matchEnum.Current);
                }
            }

            return numberFound;
        }

        /// <summary>
        /// This will pause all coroutines running on the current MEC instance until ResumeCoroutines is called.
        /// </summary>
        /// <returns>The number of coroutines that were paused.</returns>
        public static int PauseCoroutines()
        {
            return _instance == null ? 0 : _instance.PauseCoroutinesOnInstance();
        }

        /// <summary>
        /// This will pause all coroutines running on this MEC instance until ResumeCoroutinesOnInstance is called.
        /// </summary>
        /// <returns>The number of coroutines that were paused.</returns>
        public int PauseCoroutinesOnInstance()
        {
            int count = 0;
            int i;
            for (i = 0; i < _nextProcessProcessSlot; i++)
            {
                if (!ProcessPaused[i] && ProcessProcesses[i] != null)
                {
                    count++;
                    ProcessPaused[i] = true;

                    if (ProcessProcesses[i].Current > GetSegmentTime(Segment.Process))
                        ProcessProcesses[i] = _InjectDelay(ProcessProcesses[i],
                            ProcessProcesses[i].Current - GetSegmentTime(Segment.Process));
                }
            }

            for (i = 0; i < _nextDeferredProcessProcessSlot; i++)
            {
                if (!DeferredProcessPaused[i] && DeferredProcessProcesses[i] != null)
                {
                    count++;
                    DeferredProcessPaused[i] = true;

                    if (DeferredProcessProcesses[i].Current > GetSegmentTime(Segment.DeferredProcess))
                        DeferredProcessProcesses[i] = _InjectDelay(DeferredProcessProcesses[i],
                            DeferredProcessProcesses[i].Current - GetSegmentTime(Segment.DeferredProcess));
                }
            }

            for (i = 0; i < _nextPhysicsProcessProcessSlot; i++)
            {
                if (!PhysicsProcessPaused[i] && PhysicsProcessProcesses[i] != null)
                {
                    count++;
                    PhysicsProcessPaused[i] = true;

                    if (PhysicsProcessProcesses[i].Current > GetSegmentTime(Segment.PhysicsProcess))
                        PhysicsProcessProcesses[i] = _InjectDelay(PhysicsProcessProcesses[i],
                            PhysicsProcessProcesses[i].Current - GetSegmentTime(Segment.PhysicsProcess));
                }
            }

            return count;
        }

        /// <summary>
        /// This will pause any matching coroutines until ResumeCoroutines is called.
        /// </summary>
        /// <param name="handle">The handle of the coroutine to pause.</param>
        /// <returns>The number of coroutines that were paused (0 or 1).</returns>
        public static int PauseCoroutines(CoroutineHandle handle)
        {
            return ActiveInstances[handle.Key] != null ? GetInstance(handle.Key).PauseCoroutinesOnInstance(handle) : 0;
        }

        /// <summary>
        /// This will pause any matching coroutines running on this MEC instance until ResumeCoroutinesOnInstance is called.
        /// </summary>
        /// <param name="handle">The handle of the coroutine to pause.</param>
        /// <returns>The number of coroutines that were paused (0 or 1).</returns>
        public int PauseCoroutinesOnInstance(CoroutineHandle handle)
        {
            return _handleToIndex.ContainsKey(handle) && !CoindexIsNull(_handleToIndex[handle]) && !SetPause(_handleToIndex[handle], true) ? 1 : 0;
        }

        /// <summary>
        /// This will pause any matching coroutines running on the current MEC instance until ResumeCoroutines is called.
        /// </summary>
        /// <param name="tag">Any coroutines with a matching tag will be paused.</param>
        /// <returns>The number of coroutines that were paused.</returns>
        public static int PauseCoroutines(string tag)
        {
            return _instance == null ? 0 : _instance.PauseCoroutinesOnInstance(tag);
        }

        /// <summary>
        /// This will pause any matching coroutines running on this MEC instance until ResumeCoroutinesOnInstance is called.
        /// </summary>
        /// <param name="tag">Any coroutines with a matching tag will be paused.</param>
        /// <returns>The number of coroutines that were paused.</returns>
        public int PauseCoroutinesOnInstance(string tag)
        {
            if (tag == null || !_taggedProcesses.ContainsKey(tag))
                return 0;

            int count = 0;
            var matchesEnum = _taggedProcesses[tag].GetEnumerator();

            while (matchesEnum.MoveNext())
                if (!CoindexIsNull(_handleToIndex[matchesEnum.Current]) && !SetPause(_handleToIndex[matchesEnum.Current], true))
                    count++;

            return count;
        }

        /// <summary>
        /// This resumes all coroutines on the current MEC instance if they are currently paused, otherwise it has
        /// no effect.
        /// </summary>
        /// <returns>The number of coroutines that were resumed.</returns>
        public static int ResumeCoroutines()
        {
            return _instance == null ? 0 : _instance.ResumeCoroutinesOnInstance();
        }

        /// <summary>
        /// This resumes all coroutines on this MEC instance if they are currently paused, otherwise it has no effect.
        /// </summary>
        /// <returns>The number of coroutines that were resumed.</returns>
        public int ResumeCoroutinesOnInstance()
        {
            int count = 0;
            ProcessIndex coindex;
            for (coindex.i = 0, coindex.seg = Segment.Process; coindex.i < _nextProcessProcessSlot; coindex.i++)
            {
                if (ProcessPaused[coindex.i] && ProcessProcesses[coindex.i] != null)
                {
                    ProcessPaused[coindex.i] = false;
                    count++;
                }
            }

            for (coindex.i = 0, coindex.seg = Segment.DeferredProcess; coindex.i < _nextDeferredProcessProcessSlot; coindex.i++)
            {
                if (DeferredProcessPaused[coindex.i] && DeferredProcessProcesses[coindex.i] != null)
                {
                    DeferredProcessPaused[coindex.i] = false;
                    count++;
                }
            }

            for (coindex.i = 0, coindex.seg = Segment.PhysicsProcess; coindex.i < _nextPhysicsProcessProcessSlot; coindex.i++)
            {
                if (PhysicsProcessPaused[coindex.i] && PhysicsProcessProcesses[coindex.i] != null)
                {
                    PhysicsProcessPaused[coindex.i] = false;
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// This will resume any matching coroutines.
        /// </summary>
        /// <param name="handle">The handle of the coroutine to resume.</param>
        /// <returns>The number of coroutines that were resumed (0 or 1).</returns>
        public static int ResumeCoroutines(CoroutineHandle handle)
        {
            return ActiveInstances[handle.Key] != null ? GetInstance(handle.Key).ResumeCoroutinesOnInstance(handle) : 0;
        }

        /// <summary>
        /// This will resume any matching coroutines running on this MEC instance.
        /// </summary>
        /// <param name="handle">The handle of the coroutine to resume.</param>
        /// <returns>The number of coroutines that were resumed (0 or 1).</returns>
        public int ResumeCoroutinesOnInstance(CoroutineHandle handle)
        {
            return _handleToIndex.ContainsKey(handle) &&
                !CoindexIsNull(_handleToIndex[handle]) && SetPause(_handleToIndex[handle], false) ? 1 : 0;
        }

        /// <summary>
        /// This resumes any matching coroutines on the current MEC instance if they are currently paused, otherwise it has
        /// no effect.
        /// </summary>
        /// <param name="tag">Any coroutines previously paused with a matching tag will be resumend.</param>
        /// <returns>The number of coroutines that were resumed.</returns>
        public static int ResumeCoroutines(string tag)
        {
            return _instance == null ? 0 : _instance.ResumeCoroutinesOnInstance(tag);
        }

        /// <summary>
        /// This resumes any matching coroutines on this MEC instance if they are currently paused, otherwise it has no effect.
        /// </summary>
        /// <param name="tag">Any coroutines previously paused with a matching tag will be resumend.</param>
        /// <returns>The number of coroutines that were resumed.</returns>
        public int ResumeCoroutinesOnInstance(string tag)
        {
            if (tag == null || !_taggedProcesses.ContainsKey(tag))
                return 0;
            int count = 0;

            var indexesEnum = _taggedProcesses[tag].GetEnumerator();
            while (indexesEnum.MoveNext())
            {
                if (!CoindexIsNull(_handleToIndex[indexesEnum.Current]) && SetPause(_handleToIndex[indexesEnum.Current], false))
                {
                    count++;
                }
            }

            return count;
        }

        private bool UpdateTimeValues(Segment segment)
        {
            switch (segment)
            {
                case Segment.Process:
                    if (_currentProcessFrame != Engine.GetProcessFrames())
                    {

                        deltaTime = GetProcessDeltaTime();
                        _lastProcessTime += deltaTime;
                        localTime = _lastProcessTime;
                        _currentProcessFrame = Engine.GetProcessFrames();
                        return true;
                    }
                    else
                    {
                        deltaTime = GetProcessDeltaTime();
                        localTime = _lastProcessTime;
                        return false;
                    }
                case Segment.DeferredProcess:
                    if (_currentDeferredProcessFrame != Engine.GetProcessFrames())
                    {
                        deltaTime = GetProcessDeltaTime();
                        _lastDeferredProcessTime += deltaTime;
                        localTime = _lastDeferredProcessTime;
                        _currentDeferredProcessFrame = Engine.GetProcessFrames();
                        return true;
                    }
                    else
                    {
                        deltaTime = GetProcessDeltaTime();
                        localTime = _lastDeferredProcessTime;
                        return false;
                    }
                case Segment.PhysicsProcess:
                    deltaTime = GetPhysicsProcessDeltaTime();
                    _physicsProcessTime += deltaTime;
                    localTime = _physicsProcessTime;

                    if (_lastPhysicsProcessTime + 0.0001f < _physicsProcessTime)
                    {
                        _lastPhysicsProcessTime = _physicsProcessTime;
                        return true;
                    }

                    return false;
            }
            return true;
        }

        private double GetSegmentTime(Segment segment)
        {
            switch (segment)
            {
                case Segment.Process:
                    if (_currentProcessFrame == Engine.GetProcessFrames())
                        return _lastProcessTime;
                    else
                        return _lastProcessTime + GetProcessDeltaTime();
                case Segment.DeferredProcess:
                    if (_currentProcessFrame == Engine.GetProcessFrames())
                        return _lastDeferredProcessTime;
                    else
                        return _lastDeferredProcessTime + GetProcessDeltaTime();
                case Segment.PhysicsProcess:
                    return _physicsProcessTime;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Retrieves the MEC manager that corresponds to the supplied instance id.
        /// </summary>
        /// <param name="ID">The instance ID.</param>
        /// <returns>The manager, or null if not found.</returns>
        public static Timing GetInstance(byte ID)
        {
            if (ID >= 0x10)
                return null;
            return ActiveInstances[ID];
        }

        private void AddTag(string tag, CoroutineHandle coindex)
        {
            _processTags.Add(coindex, tag);

            if (_taggedProcesses.ContainsKey(tag))
                _taggedProcesses[tag].Add(coindex);
            else
                _taggedProcesses.Add(tag, new HashSet<CoroutineHandle> { coindex });
        }

        private void RemoveTag(CoroutineHandle coindex)
        {
            if (_processTags.ContainsKey(coindex))
            {
                if (_taggedProcesses[_processTags[coindex]].Count > 1)
                    _taggedProcesses[_processTags[coindex]].Remove(coindex);
                else
                    _taggedProcesses.Remove(_processTags[coindex]);

                _processTags.Remove(coindex);
            }
        }

        /// <returns>Whether it was already null.</returns>
        private bool Nullify(ProcessIndex coindex)
        {
            bool retVal;

            switch (coindex.seg)
            {
                case Segment.Process:
                    retVal = ProcessProcesses[coindex.i] != null;
                    ProcessProcesses[coindex.i] = null;
                    return retVal;
                case Segment.PhysicsProcess:
                    retVal = PhysicsProcessProcesses[coindex.i] != null;
                    PhysicsProcessProcesses[coindex.i] = null;
                    return retVal;
                case Segment.DeferredProcess:
                    retVal = DeferredProcessProcesses[coindex.i] != null;
                    DeferredProcessProcesses[coindex.i] = null;
                    return retVal;
                default:
                    return false;
            }
        }

        private IEnumerator<double> CoindexExtract(ProcessIndex coindex)
        {
            IEnumerator<double> retVal;

            switch (coindex.seg)
            {
                case Segment.Process:
                    retVal = ProcessProcesses[coindex.i];
                    ProcessProcesses[coindex.i] = null;
                    return retVal;
                case Segment.PhysicsProcess:
                    retVal = PhysicsProcessProcesses[coindex.i];
                    PhysicsProcessProcesses[coindex.i] = null;
                    return retVal;
                case Segment.DeferredProcess:
                    retVal = DeferredProcessProcesses[coindex.i];
                    DeferredProcessProcesses[coindex.i] = null;
                    return retVal;
                default:
                    return null;
            }
        }

        private IEnumerator<double> CoindexPeek(ProcessIndex coindex)
        {
            switch (coindex.seg)
            {
                case Segment.Process:
                    return ProcessProcesses[coindex.i];
                case Segment.PhysicsProcess:
                    return PhysicsProcessProcesses[coindex.i];
                case Segment.DeferredProcess:
                    return DeferredProcessProcesses[coindex.i];
                default:
                    return null;
            }
        }

        private bool CoindexIsNull(ProcessIndex coindex)
        {
            switch (coindex.seg)
            {
                case Segment.Process:
                    return ProcessProcesses[coindex.i] == null;
                case Segment.PhysicsProcess:
                    return PhysicsProcessProcesses[coindex.i] == null;
                case Segment.DeferredProcess:
                    return DeferredProcessProcesses[coindex.i] == null;
                default:
                    return true;
            }
        }

        private bool SetPause(ProcessIndex coindex, bool newPausedState)
        {
            if (CoindexPeek(coindex) == null)
                return false;

            bool isPaused;

            switch (coindex.seg)
            {
                case Segment.Process:
                    isPaused = ProcessPaused[coindex.i];
                    ProcessPaused[coindex.i] = newPausedState;

                    if (newPausedState && ProcessProcesses[coindex.i].Current > GetSegmentTime(coindex.seg))
                        ProcessProcesses[coindex.i] = _InjectDelay(ProcessProcesses[coindex.i],
                            ProcessProcesses[coindex.i].Current - GetSegmentTime(coindex.seg));

                    return isPaused;
                case Segment.PhysicsProcess:
                    isPaused = PhysicsProcessPaused[coindex.i];
                    PhysicsProcessPaused[coindex.i] = newPausedState;

                    if (newPausedState && PhysicsProcessProcesses[coindex.i].Current > GetSegmentTime(coindex.seg))
                        PhysicsProcessProcesses[coindex.i] = _InjectDelay(PhysicsProcessProcesses[coindex.i],
                            PhysicsProcessProcesses[coindex.i].Current - GetSegmentTime(coindex.seg));

                    return isPaused;
                case Segment.DeferredProcess:
                    isPaused = DeferredProcessPaused[coindex.i];
                    DeferredProcessPaused[coindex.i] = newPausedState;

                    if (newPausedState && DeferredProcessProcesses[coindex.i].Current > GetSegmentTime(coindex.seg))
                        DeferredProcessProcesses[coindex.i] = _InjectDelay(DeferredProcessProcesses[coindex.i],
                            DeferredProcessProcesses[coindex.i].Current - GetSegmentTime(coindex.seg));

                    return isPaused;
                default:
                    return false;
            }
        }

        private bool SetHeld(ProcessIndex coindex, bool newHeldState)
        {
            if (CoindexPeek(coindex) == null)
                return false;

            bool isHeld;

            switch (coindex.seg)
            {
                case Segment.Process:
                    isHeld = ProcessHeld[coindex.i];
                    ProcessHeld[coindex.i] = newHeldState;

                    if (newHeldState && ProcessProcesses[coindex.i].Current > GetSegmentTime(coindex.seg))
                        ProcessProcesses[coindex.i] = _InjectDelay(ProcessProcesses[coindex.i],
                            ProcessProcesses[coindex.i].Current - GetSegmentTime(coindex.seg));

                    return isHeld;
                case Segment.PhysicsProcess:
                    isHeld = PhysicsProcessHeld[coindex.i];
                    PhysicsProcessHeld[coindex.i] = newHeldState;

                    if (newHeldState && PhysicsProcessProcesses[coindex.i].Current > GetSegmentTime(coindex.seg))
                        PhysicsProcessProcesses[coindex.i] = _InjectDelay(PhysicsProcessProcesses[coindex.i],
                            PhysicsProcessProcesses[coindex.i].Current - GetSegmentTime(coindex.seg));

                    return isHeld;
                case Segment.DeferredProcess:
                    isHeld = DeferredProcessHeld[coindex.i];
                    DeferredProcessHeld[coindex.i] = newHeldState;

                    if (newHeldState && DeferredProcessProcesses[coindex.i].Current > GetSegmentTime(coindex.seg))
                        DeferredProcessProcesses[coindex.i] = _InjectDelay(DeferredProcessProcesses[coindex.i],
                            DeferredProcessProcesses[coindex.i].Current - GetSegmentTime(coindex.seg));

                    return isHeld;
                default:
                    return false;
            }
        }

        private IEnumerator<double> _InjectDelay(IEnumerator<double> proc, double delayTime)
        {
            yield return WaitForSecondsOnInstance(delayTime);

            _tmpRef = proc;
            ReplacementFunction = ReturnTmpRefForRepFunc;
            yield return double.NaN;
        }

        private bool CoindexIsPaused(ProcessIndex coindex)
        {
            switch (coindex.seg)
            {
                case Segment.Process:
                    return ProcessPaused[coindex.i];
                case Segment.PhysicsProcess:
                    return PhysicsProcessPaused[coindex.i];
                case Segment.DeferredProcess:
                    return DeferredProcessPaused[coindex.i];
                default:
                    return false;
            }
        }

        private bool CoindexIsHeld(ProcessIndex coindex)
        {
            switch (coindex.seg)
            {
                case Segment.Process:
                    return ProcessHeld[coindex.i];
                case Segment.PhysicsProcess:
                    return PhysicsProcessHeld[coindex.i];
                case Segment.DeferredProcess:
                    return DeferredProcessHeld[coindex.i];
                default:
                    return false;
            }
        }

        private void CoindexReplace(ProcessIndex coindex, IEnumerator<double> replacement)
        {
            switch (coindex.seg)
            {
                case Segment.Process:
                    ProcessProcesses[coindex.i] = replacement;
                    return;
                case Segment.PhysicsProcess:
                    PhysicsProcessProcesses[coindex.i] = replacement;
                    return;
                case Segment.DeferredProcess:
                    DeferredProcessProcesses[coindex.i] = replacement;
                    return;
            }
        }

        /// <summary>
        /// Use "yield return Timing.WaitForSeconds(time);" to wait for the specified number of seconds.
        /// </summary>
        /// <param name="waitTime">Number of seconds to wait.</param>
        public static double WaitForSeconds(double waitTime)
        {
            if (double.IsNaN(waitTime)) waitTime = 0f;
            return Instance.localTime + waitTime;
        }

        /// <summary>
        /// Use "yield return timingInstance.WaitForSecondsOnInstance(time);" to wait for the specified number of seconds.
        /// </summary>
        /// <param name="waitTime">Number of seconds to wait.</param>
        public double WaitForSecondsOnInstance(double waitTime)
        {
            if (double.IsNaN(waitTime)) waitTime = 0f;
            return localTime + waitTime;
        }

        /// <summary>
        /// Use the command "yield return Timing.WaitUntilDone(otherCoroutine);" to pause the current 
        /// coroutine until otherCoroutine is done.
        /// </summary>
        /// <param name="otherCoroutine">The coroutine to pause for.</param>
        public static double WaitUntilDone(CoroutineHandle otherCoroutine)
        {
            return WaitUntilDone(otherCoroutine, true);
        }

        /// <summary>
        /// Use the command "yield return Timing.WaitUntilDone(otherCoroutine, false);" to pause the current 
        /// coroutine until otherCoroutine is done, supressing warnings.
        /// </summary>
        /// <param name="otherCoroutine">The coroutine to pause for.</param>
        /// <param name="warnOnIssue">Post a warning to the console if no hold action was actually performed.</param>
        public static double WaitUntilDone(CoroutineHandle otherCoroutine, bool warnOnIssue)
        {
            Timing inst = GetInstance(otherCoroutine.Key);

            if (inst != null && inst._handleToIndex.ContainsKey(otherCoroutine))
            {
                if (inst.CoindexIsNull(inst._handleToIndex[otherCoroutine]))
                    return 0f;

                if (!inst._waitingTriggers.ContainsKey(otherCoroutine))
                {
                    inst.CoindexReplace(inst._handleToIndex[otherCoroutine],
                        inst._StartWhenDone(otherCoroutine, inst.CoindexPeek(inst._handleToIndex[otherCoroutine])));
                    inst._waitingTriggers.Add(otherCoroutine, new HashSet<CoroutineHandle>());
                }

                if (inst.currentCoroutine == otherCoroutine)
                {
                    if (warnOnIssue)
                        GD.PrintErr("A coroutine cannot wait for itself.");
                    return WaitForOneFrame;
                }
                if (!inst.currentCoroutine.IsValid)
                {
                    if (warnOnIssue)
                        GD.PrintErr("The two coroutines are not running on the same MEC instance.");
                    return WaitForOneFrame;
                }

                inst._waitingTriggers[otherCoroutine].Add(inst.currentCoroutine);
                if (!inst._allWaiting.Contains(inst.currentCoroutine))
                    inst._allWaiting.Add(inst.currentCoroutine);
                inst.SetHeld(inst._handleToIndex[inst.currentCoroutine], true);
                inst.SwapToLast(otherCoroutine, inst.currentCoroutine);

                return double.NaN;
            }

            if (warnOnIssue)
                GD.PrintErr("WaitUntilDone cannot hold: The coroutine handle that was passed in is invalid.\n" + otherCoroutine);
            return WaitForOneFrame;
        }

        private IEnumerator<double> _StartWhenDone(CoroutineHandle handle, IEnumerator<double> proc)
        {
            if (!_waitingTriggers.ContainsKey(handle)) yield break;

            try
            {
                if (proc.Current > localTime)
                    yield return proc.Current;

                while (proc.MoveNext())
                    yield return proc.Current;
            }
            finally
            {
                CloseWaitingProcess(handle);
            }
        }

        private void SwapToLast(CoroutineHandle firstHandle, CoroutineHandle lastHandle)
        {
            if (firstHandle.Key != lastHandle.Key)
                return;

            ProcessIndex firstIndex = _handleToIndex[firstHandle];
            ProcessIndex lastIndex = _handleToIndex[lastHandle];

            if (firstIndex.seg != lastIndex.seg || firstIndex.i < lastIndex.i)
                return;

            IEnumerator<double> tempCoptr = CoindexPeek(firstIndex);
            CoindexReplace(firstIndex, CoindexPeek(lastIndex));
            CoindexReplace(lastIndex, tempCoptr);

            _indexToHandle[firstIndex] = lastHandle;
            _indexToHandle[lastIndex] = firstHandle;
            _handleToIndex[firstHandle] = lastIndex;
            _handleToIndex[lastHandle] = firstIndex;
            bool tmpB = SetPause(firstIndex, CoindexIsPaused(lastIndex));
            SetPause(lastIndex, tmpB);
            tmpB = SetHeld(firstIndex, CoindexIsHeld(lastIndex));
            SetHeld(lastIndex, tmpB);

            if (_waitingTriggers.ContainsKey(lastHandle))
            {
                var trigsEnum = _waitingTriggers[lastHandle].GetEnumerator();
                while (trigsEnum.MoveNext())
                    SwapToLast(lastHandle, trigsEnum.Current);
            }

            if (_allWaiting.Contains(firstHandle))
            {
                var keyEnum = _waitingTriggers.GetEnumerator();
                while (keyEnum.MoveNext())
                {
                    var valueEnum = keyEnum.Current.Value.GetEnumerator();
                    while (valueEnum.MoveNext())
                        if (valueEnum.Current == firstHandle)
                            SwapToLast(keyEnum.Current.Key, firstHandle);
                }
            }
        }

        private void CloseWaitingProcess(CoroutineHandle handle)
        {
            if (!_waitingTriggers.ContainsKey(handle)) return;

            var tasksEnum = _waitingTriggers[handle].GetEnumerator();
            _waitingTriggers.Remove(handle);

            while (tasksEnum.MoveNext())
            {
                if (_handleToIndex.ContainsKey(tasksEnum.Current) && !HandleIsInWaitingList(tasksEnum.Current))
                {
                    SetHeld(_handleToIndex[tasksEnum.Current], false);
                    _allWaiting.Remove(tasksEnum.Current);
                }
            }
        }

        private bool HandleIsInWaitingList(CoroutineHandle handle)
        {
            var triggersEnum = _waitingTriggers.GetEnumerator();
            while (triggersEnum.MoveNext())
                if (triggersEnum.Current.Value.Contains(handle))
                    return true;

            return false;
        }

        private static IEnumerator<double> ReturnTmpRefForRepFunc(IEnumerator<double> coptr, CoroutineHandle handle)
        {
            return _tmpRef as IEnumerator<double>;
        }

        /// <summary>
        /// Keeps this coroutine from executing until UnlockCoroutine is called with a matching key.
        /// </summary>
        /// <param name="coroutine">The handle to the coroutine to be locked.</param>
        /// <param name="key">The key to use. A new key can be generated by calling "new CoroutineHandle(0)".</param>
        /// <returns>Whether the lock was successful.</returns>
        public bool LockCoroutine(CoroutineHandle coroutine, CoroutineHandle key)
        {
            if (coroutine.Key != _instanceID || key == new CoroutineHandle() || key.Key != 0)
                return false;

            if (!_waitingTriggers.ContainsKey(key))
                _waitingTriggers.Add(key, new HashSet<CoroutineHandle> { coroutine });
            else
                _waitingTriggers[key].Add(coroutine);

            _allWaiting.Add(coroutine);

            SetHeld(_handleToIndex[coroutine], true);

            return true;
        }

        /// <summary>
        /// Unlocks a coroutine that has been locked, so long as the key matches.
        /// </summary>
        /// <param name="coroutine">The handle to the coroutine to be unlocked.</param>
        /// <param name="key">The key that the coroutine was previously locked with.</param>
        /// <returns>Whether the coroutine was successfully unlocked.</returns>
        public bool UnlockCoroutine(CoroutineHandle coroutine, CoroutineHandle key)
        {
            if (coroutine.Key != _instanceID || key == new CoroutineHandle() ||
                !_handleToIndex.ContainsKey(coroutine) || !_waitingTriggers.ContainsKey(key))
                return false;

            if (_waitingTriggers[key].Count == 1)
                _waitingTriggers.Remove(key);
            else
                _waitingTriggers[key].Remove(coroutine);

            if (!HandleIsInWaitingList(coroutine))
            {
                SetHeld(_handleToIndex[coroutine], false);
                _allWaiting.Remove(coroutine);
            }

            return true;
        }

        /// <summary>
        /// Calls the specified action after current process step is completed.
        /// </summary>
        /// <param name="action">The action to call.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallDeferred(System.Action action)
        {
            return action == null ? new CoroutineHandle() : RunCoroutine(Instance._DelayedCall(0, action, null), Segment.DeferredProcess);
        }

        /// <summary>
        /// Calls the specified action after current process step is completed.
        /// </summary>
        /// <param name="action">The action to call.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallDeferredOnInstance(System.Action action)
        {
            return action == null ? new CoroutineHandle() : RunCoroutine(_DelayedCall(0, action, null), Segment.DeferredProcess);
        }

        /// <summary>
        /// Calls the specified action after a specified number of seconds.
        /// </summary>
        /// <param name="delay">The number of seconds to wait before calling the action.</param>
        /// <param name="action">The action to call.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallDelayed(double delay, System.Action action)
        {
            return action == null ? new CoroutineHandle() : RunCoroutine(Instance._DelayedCall(delay, action, null));
        }

        /// <summary>
        /// Calls the specified action after a specified number of seconds.
        /// </summary>
        /// <param name="delay">The number of seconds to wait before calling the action.</param>
        /// <param name="action">The action to call.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallDelayedOnInstance(double delay, System.Action action)
        {
            return action == null ? new CoroutineHandle() : RunCoroutineOnInstance(_DelayedCall(delay, action, null));
        }

        /// <summary>
        /// Calls the specified action after a specified number of seconds.
        /// </summary>
        /// <param name="delay">The number of seconds to wait before calling the action.</param>
        /// <param name="action">The action to call.</param>
        /// <param name="cancelWith">A Node that will be checked to make sure it hasn't been destroyed before calling the action.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallDelayed(double delay, System.Action action, Node cancelWith)
        {
            return action == null ? new CoroutineHandle() : RunCoroutine(Instance._DelayedCall(delay, action, cancelWith));
        }

        /// <summary>
        /// Calls the specified action after a specified number of seconds.
        /// </summary>
        /// <param name="delay">The number of seconds to wait before calling the action.</param>
        /// <param name="action">The action to call.</param>
        /// <param name="cancelWith">A Node that will be checked to make sure it hasn't been destroyed before calling the action.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallDelayedOnInstance(double delay, System.Action action, Node cancelWith)
        {
            return action == null ? new CoroutineHandle() : RunCoroutineOnInstance(_DelayedCall(delay, action, cancelWith));
        }

        /// <summary>
        /// Calls the specified action after a specified number of seconds.
        /// </summary>
        /// <param name="delay">The number of seconds to wait before calling the action.</param>
        /// <param name="action">The action to call.</param>
        /// <param name="segment">The timing segment that the call should be made in.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallDelayed(double delay, Segment segment, System.Action action)
        {
            return action == null ? new CoroutineHandle() : RunCoroutine(Instance._DelayedCall(delay, action, null), segment);
        }

        /// <summary>
        /// Calls the specified action after a specified number of seconds.
        /// </summary>
        /// <param name="delay">The number of seconds to wait before calling the action.</param>
        /// <param name="action">The action to call.</param>
        /// <param name="segment">The timing segment that the call should be made in.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallDelayedOnInstance(double delay, Segment segment, System.Action action)
        {
            return action == null ? new CoroutineHandle() : RunCoroutineOnInstance(_DelayedCall(delay, action, null), segment);
        }

        /// <summary>
        /// Calls the specified action after a specified number of seconds.
        /// </summary>
        /// <param name="delay">The number of seconds to wait before calling the action.</param>
        /// <param name="action">The action to call.</param>
        /// <param name="node">A Node that will be checked to make sure it hasn't been destroyed 
        /// before calling the action.</param>
        /// <param name="segment">The timing segment that the call should be made in.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallDelayed(double delay, Segment segment, System.Action action, Node node)
        {
            return action == null ? new CoroutineHandle() : RunCoroutine(Instance._DelayedCall(delay, action, node), segment);
        }

        /// <summary>
        /// Calls the specified action after a specified number of seconds.
        /// </summary>
        /// <param name="delay">The number of seconds to wait before calling the action.</param>
        /// <param name="action">The action to call.</param>
        /// <param name="node">A Node that will be tagged onto the coroutine and checked to make sure it hasn't been destroyed 
        /// before calling the action.</param>
        /// <param name="segment">The timing segment that the call should be made in.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallDelayedOnInstance(double delay, Segment segment, System.Action action, Node node)
        {
            return action == null ? new CoroutineHandle() : RunCoroutineOnInstance(_DelayedCall(delay, action, node), segment);
        }

        private IEnumerator<double> _DelayedCall(double delay, System.Action action, Node cancelWith)
        {
            yield return WaitForSecondsOnInstance(delay);

            if (ReferenceEquals(cancelWith, null) || cancelWith != null)
                action();
        }

        /// <summary>
        /// Calls the supplied action at the given rate for a given number of seconds.
        /// </summary>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="period">The amount of time between calls.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallPeriodically(double timeframe, double period, System.Action action, System.Action onDone = null)
        {
            return action == null ? new CoroutineHandle() : RunCoroutine(Instance._CallContinuously(timeframe, period, action, onDone), Segment.Process);
        }

        /// <summary>
        /// Calls the supplied action at the given rate for a given number of seconds.
        /// </summary>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="period">The amount of time between calls.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallPeriodicallyOnInstance(double timeframe, double period, System.Action action, System.Action onDone = null)
        {
            return action == null ? new CoroutineHandle() : RunCoroutineOnInstance(_CallContinuously(timeframe, period, action, onDone), Segment.Process);
        }

        /// <summary>
        /// Calls the supplied action at the given rate for a given number of seconds.
        /// </summary>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="period">The amount of time between calls.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="segment">The timing segment to run in.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallPeriodically(double timeframe, double period, System.Action action, Segment segment, System.Action onDone = null)
        {
            return action == null ? new CoroutineHandle() : RunCoroutine(Instance._CallContinuously(timeframe, period, action, onDone), segment);
        }

        /// <summary>
        /// Calls the supplied action at the given rate for a given number of seconds.
        /// </summary>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="period">The amount of time between calls.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="segment">The timing segment to run in.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallPeriodicallyOnInstance(double timeframe, double period, System.Action action, Segment segment, System.Action onDone = null)
        {
            return action == null ? new CoroutineHandle() : RunCoroutineOnInstance(_CallContinuously(timeframe, period, action, onDone), segment);
        }

        /// <summary>
        /// Calls the supplied action at the given rate for a given number of seconds.
        /// </summary>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallContinuously(double timeframe, System.Action action, System.Action onDone = null)
        {
            return action == null ? new CoroutineHandle() : RunCoroutine(Instance._CallContinuously(timeframe, 0f, action, onDone), Segment.Process);
        }

        /// <summary>
        /// Calls the supplied action at the given rate for a given number of seconds.
        /// </summary>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallContinuouslyOnInstance(double timeframe, System.Action action, System.Action onDone = null)
        {
            return action == null ? new CoroutineHandle() : RunCoroutineOnInstance(_CallContinuously(timeframe, 0f, action, onDone), Segment.Process);
        }

        /// <summary>
        /// Calls the supplied action every frame for a given number of seconds.
        /// </summary>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="timing">The timing segment to run in.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallContinuously(double timeframe, System.Action action, Segment timing, System.Action onDone = null)
        {
            return action == null ? new CoroutineHandle() : RunCoroutine(Instance._CallContinuously(timeframe, 0f, action, onDone), timing);
        }

        /// <summary>
        /// Calls the supplied action every frame for a given number of seconds.
        /// </summary>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="timing">The timing segment to run in.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallContinuouslyOnInstance(double timeframe, System.Action action, Segment timing, System.Action onDone = null)
        {
            return action == null ? new CoroutineHandle() : RunCoroutineOnInstance(_CallContinuously(timeframe, 0f, action, onDone), timing);
        }

        private IEnumerator<double> _CallContinuously(double timeframe, double period, System.Action action, System.Action onDone)
        {
            double startTime = localTime;
            while (localTime <= startTime + timeframe)
            {
                yield return WaitForSecondsOnInstance(period);

                action();
            }

            if (onDone != null)
                onDone();
        }

        /// <summary>
        /// Calls the supplied action at the given rate for a given number of seconds.
        /// </summary>
        /// <param name="reference">A value that will be passed in to the supplied action each period.</param>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="period">The amount of time between calls.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallPeriodically<T>
            (T reference, double timeframe, double period, System.Action<T> action, System.Action<T> onDone = null)
        {
            return action == null ? new CoroutineHandle() :
                RunCoroutine(Instance._CallContinuously(reference, timeframe, period, action, onDone), Segment.Process);
        }

        /// <summary>
        /// Calls the supplied action at the given rate for a given number of seconds.
        /// </summary>
        /// <param name="reference">A value that will be passed in to the supplied action each period.</param>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="period">The amount of time between calls.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallPeriodicallyOnInstance<T>
            (T reference, double timeframe, double period, System.Action<T> action, System.Action<T> onDone = null)
        {
            return action == null ? new CoroutineHandle() :
                RunCoroutineOnInstance(_CallContinuously(reference, timeframe, period, action, onDone), Segment.Process);
        }

        /// <summary>
        /// Calls the supplied action at the given rate for a given number of seconds.
        /// </summary>
        /// <param name="reference">A value that will be passed in to the supplied action each period.</param>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="period">The amount of time between calls.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="timing">The timing segment to run in.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallPeriodically<T>(T reference, double timeframe, double period, System.Action<T> action,
            Segment timing, System.Action<T> onDone = null)
        {
            return action == null ? new CoroutineHandle() :
                RunCoroutine(Instance._CallContinuously(reference, timeframe, period, action, onDone), timing);
        }

        /// <summary>
        /// Calls the supplied action at the given rate for a given number of seconds.
        /// </summary>
        /// <param name="reference">A value that will be passed in to the supplied action each period.</param>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="period">The amount of time between calls.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="timing">The timing segment to run in.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallPeriodicallyOnInstance<T>(T reference, double timeframe, double period, System.Action<T> action,
            Segment timing, System.Action<T> onDone = null)
        {
            return action == null ? new CoroutineHandle() :
                RunCoroutineOnInstance(_CallContinuously(reference, timeframe, period, action, onDone), timing);
        }

        /// <summary>
        /// Calls the supplied action every frame for a given number of seconds.
        /// </summary>
        /// <param name="reference">A value that will be passed in to the supplied action each frame.</param>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallContinuously<T>(T reference, double timeframe, System.Action<T> action, System.Action<T> onDone = null)
        {
            return action == null ? new CoroutineHandle() :
                RunCoroutine(Instance._CallContinuously(reference, timeframe, 0f, action, onDone), Segment.Process);
        }

        /// <summary>
        /// Calls the supplied action every frame for a given number of seconds.
        /// </summary>
        /// <param name="reference">A value that will be passed in to the supplied action each frame.</param>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallContinuouslyOnInstance<T>(T reference, double timeframe, System.Action<T> action, System.Action<T> onDone = null)
        {
            return action == null ? new CoroutineHandle() :
                RunCoroutineOnInstance(_CallContinuously(reference, timeframe, 0f, action, onDone), Segment.Process);
        }

        /// <summary>
        /// Calls the supplied action every frame for a given number of seconds.
        /// </summary>
        /// <param name="reference">A value that will be passed in to the supplied action each frame.</param>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="timing">The timing segment to run in.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public static CoroutineHandle CallContinuously<T>(T reference, double timeframe, System.Action<T> action,
            Segment timing, System.Action<T> onDone = null)
        {
            return action == null ? new CoroutineHandle() :
                RunCoroutine(Instance._CallContinuously(reference, timeframe, 0f, action, onDone), timing);
        }

        /// <summary>
        /// Calls the supplied action every frame for a given number of seconds.
        /// </summary>
        /// <param name="reference">A value that will be passed in to the supplied action each frame.</param>
        /// <param name="timeframe">The number of seconds that this function should run.</param>
        /// <param name="action">The action to call every frame.</param>
        /// <param name="timing">The timing segment to run in.</param>
        /// <param name="onDone">An optional action to call when this function finishes.</param>
        /// <returns>The handle to the coroutine that is started by this function.</returns>
        public CoroutineHandle CallContinuouslyOnInstance<T>(T reference, double timeframe, System.Action<T> action,
            Segment timing, System.Action<T> onDone = null)
        {
            return action == null ? new CoroutineHandle() :
                RunCoroutineOnInstance(_CallContinuously(reference, timeframe, 0f, action, onDone), timing);
        }

        private IEnumerator<double> _CallContinuously<T>(T reference, double timeframe, double period,
            System.Action<T> action, System.Action<T> onDone = null)
        {
            double startTime = localTime;
            while (localTime <= startTime + timeframe)
            {
                yield return WaitForSecondsOnInstance(period);

                action(reference);
            }

            if (onDone != null)
                onDone(reference);
        }

        private struct ProcessIndex : System.IEquatable<ProcessIndex>
        {
            public Segment seg;
            public int i;

            public bool Equals(ProcessIndex other)
            {
                return seg == other.seg && i == other.i;
            }

            public override bool Equals(object other)
            {
                if (other is ProcessIndex)
                    return Equals((ProcessIndex)other);
                return false;
            }

            public static bool operator ==(ProcessIndex a, ProcessIndex b)
            {
                return a.seg == b.seg && a.i == b.i;
            }

            public static bool operator !=(ProcessIndex a, ProcessIndex b)
            {
                return a.seg != b.seg || a.i != b.i;
            }

            public override int GetHashCode()
            {
                return (((int)seg - 2) * (int.MaxValue / 3)) + i;
            }
        }
    }

    /// <summary>
    /// The timing segment that a coroutine is running in or should be run in.
    /// </summary>
    public enum Segment
    {
        /// <summary>
        /// Sometimes returned as an error state
        /// </summary>
        Invalid = -1,
        /// <summary>
        /// This is the default timing segment
        /// </summary>
        Process,
        /// <summary>
        /// This is primarily used for physics calculations
        /// </summary>
        PhysicsProcess,
        /// <summary>
        /// This is run immediately after update
        /// </summary>
        DeferredProcess
    }

    /// <summary>
    /// A handle for a MEC coroutine.
    /// </summary>
    public struct CoroutineHandle : System.IEquatable<CoroutineHandle>
    {
        private const byte ReservedSpace = 0x0F;
        private readonly static int[] NextIndex = { ReservedSpace + 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private readonly int _id;

        public byte Key { get { return (byte)(_id & ReservedSpace); } }

        public CoroutineHandle(byte ind)
        {
            if (ind > ReservedSpace)
                ind -= ReservedSpace;

            _id = NextIndex[ind] + ind;
            NextIndex[ind] += ReservedSpace + 1;
        }

        public bool Equals(CoroutineHandle other)
        {
            return _id == other._id;
        }

        public override bool Equals(object other)
        {
            if (other is CoroutineHandle)
                return Equals((CoroutineHandle)other);
            return false;
        }

        public static bool operator ==(CoroutineHandle a, CoroutineHandle b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(CoroutineHandle a, CoroutineHandle b)
        {
            return a._id != b._id;
        }

        public override int GetHashCode()
        {
            return _id;
        }

        /// <summary>
        /// Is true if this handle may have been a valid handle at some point. (i.e. is not an uninitialized handle, error handle, or a key to a coroutine lock)
        /// </summary>
        public bool IsValid
        {
            get { return Key != 0; }
        }
    }

    public static class MECExtensionMethods1
    {
        /// <summary>
        /// Run a new coroutine in the Process segment.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public static CoroutineHandle RunCoroutine(this IEnumerator<double> coroutine)
        {
            return Timing.RunCoroutine(coroutine);
        }

        /// <summary>
        /// Run a new coroutine in the Process segment.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <param name="tag">An optional tag to attach to the coroutine which can later be used to identify this coroutine.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public static CoroutineHandle RunCoroutine(this IEnumerator<double> coroutine, string tag)
        {
            return Timing.RunCoroutine(coroutine, tag);
        }

        /// <summary>
        /// Run a new coroutine.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <param name="segment">The segment that the coroutine should run in.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public static CoroutineHandle RunCoroutine(this IEnumerator<double> coroutine, Segment segment)
        {
            return Timing.RunCoroutine(coroutine, segment);
        }

        /// <summary>
        /// Run a new coroutine.
        /// </summary>
        /// <param name="coroutine">The new coroutine's handle.</param>
        /// <param name="segment">The segment that the coroutine should run in.</param>
        /// <param name="tag">An optional tag to attach to the coroutine which can later be used to identify this coroutine.</param>
        /// <returns>The coroutine's handle, which can be used for Wait and Kill operations.</returns>
        public static CoroutineHandle RunCoroutine(this IEnumerator<double> coroutine, Segment segment, string tag)
        {
            return Timing.RunCoroutine(coroutine, segment, tag);
        }
    }
}

public static class MECExtensionMethods2
{
    /// <summary>
    /// Cancels this coroutine when the supplied game object is destroyed or made inactive.
    /// </summary>
    /// <param name="coroutine">The coroutine handle to act upon.</param>
    /// <param name="node">The Node to test.</param>
    /// <returns>The modified coroutine handle.</returns>
    public static IEnumerator<double> CancelWith(this IEnumerator<double> coroutine, Node node)
    {
        while (MEC.Timing.MainThread != System.Threading.Thread.CurrentThread || (IsNodeAlive(node) && coroutine.MoveNext()))
            yield return coroutine.Current;
    }

    /// <summary>
    /// Cancels this coroutine when the supplied game objects are destroyed or made inactive.
    /// </summary>
    /// <param name="coroutine">The coroutine handle to act upon.</param>
    /// <param name="node1">The first Node to test.</param>
    /// <param name="node2">The second Node to test</param>
    /// <returns>The modified coroutine handle.</returns>
    public static IEnumerator<double> CancelWith(this IEnumerator<double> coroutine, Node node1, Node node2)
    {
        while (MEC.Timing.MainThread != System.Threading.Thread.CurrentThread || (IsNodeAlive(node1) && IsNodeAlive(node2) && coroutine.MoveNext()))
            yield return coroutine.Current;
    }

    /// <summary>
    /// Cancels this coroutine when the supplied game objects are destroyed or made inactive.
    /// </summary>
    /// <param name="coroutine">The coroutine handle to act upon.</param>
    /// <param name="node1">The first Node to test.</param>
    /// <param name="node2">The second Node to test</param>
    /// <param name="node3">The third Node to test.</param>
    /// <returns>The modified coroutine handle.</returns>
    public static IEnumerator<double> CancelWith(this IEnumerator<double> coroutine,
        Node node1, Node node2, Node node3)
    {
        while (MEC.Timing.MainThread != System.Threading.Thread.CurrentThread || (IsNodeAlive(node1) && IsNodeAlive(node2) && IsNodeAlive(node3) && coroutine.MoveNext()))
            yield return coroutine.Current;
    }

    /// <summary>
    /// Checks whether a node exists, has not been deleted, and is in a tree
    /// </summary>
    /// <returns></returns>
    private static bool IsNodeAlive(Node node)
    {
        return node != null && !node.IsQueuedForDeletion() && node.IsInsideTree();
    }
}
