﻿using System;
using System.Collections.Generic;
using System.Linq;

using com.spacepuppy.Utils;

namespace com.spacepuppy.SPInput.Unity
{

    /// <summary>
    /// Use this to poll for an InputSignature defined by the user. You start by creating this object, starting it and waiting for it to complete. 
    /// It can also be used as a yield instruction to wait for its completion.<para/>
    /// Behaviour of this routine:<para/>
    /// 1) The system expects the player to press in the 'up' direction to register an axis. If they press down it interprets this as wanting inverted controls. <para/>
    /// 2) Use the PollAsTrigger property to control registering only positive presses for triggers. <para/>
    /// 3) If PollButtons or PollKeyboard is set true, it will register the first button press as the positive value, and the second as the negative. 
    /// Unless PollAsTrigger is true, in which case it immediately registers as a trigger axis on first button press.
    /// </summary>
    /// <typeparam name="TButton"></typeparam>
    /// <typeparam name="TAxis"></typeparam>
    public class PollingAxisSignatureRoutine : IRadicalWaitHandle
    {

        public delegate bool AxisPollingCallback(PollingAxisSignatureRoutine targ, out AxisDelegate del);
        public delegate bool ButtonPollingCallback(PollingAxisSignatureRoutine targ, out ButtonDelegate del);

        private enum State
        {
            Unknown,
            Running,
            Cancelled,
            Complete
        }

        #region Fields

        private State _state;
        private RadicalCoroutine _routine;

        #endregion

        #region CONSTRUCTOR

        public PollingAxisSignatureRoutine()
        {
            this.PollButtons = false;
            this.PollKeyboard = false;
            this.PollFromStandardSPInputs = true;
            this.Joystick = Joystick.All;
            this.AxisConsideration = AxleValueConsideration.Absolute;
            this.AxisPollingDeadZone = InputUtil.DEFAULT_AXLEBTNDEADZONE;
            this.ButtonPressMonitorDuration = 5.0f;
            this.AllowMouseAsAxis = false;
            this.CancelKey = UnityEngine.KeyCode.Escape;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The resulting ButtonDelegate that can be used to poll for the input.
        /// </summary>
        public AxisDelegate DelegateResult
        {
            get;
            set;
        }

        /// <summary>
        /// Joystick # to poll for. <param/>
        /// Default: <see cref="Joystick.All"/>
        /// </summary>
        public Joystick Joystick
        {
            get;
            set;
        }

        /// <summary>
        /// Should we poll all the standard inputs defined by SPInputDirect. 
        /// If no profiles are defined and this is false, than no gamepads will be polled for. <para/>
        /// Default: True
        /// </summary>
        public bool PollFromStandardSPInputs
        {
            get;
            set;
        }

        /// <summary>
        /// Should we poll button presses. <para/>
        /// Default: False
        /// </summary>
        public bool PollButtons
        {
            get;
            set;
        }

        /// <summary>
        /// Should we poll key presses. <para/>
        /// Default: False
        /// </summary>
        public bool PollKeyboard
        {
            get;
            set;
        }

        /// <summary>
        /// Should we poll for axis tilts. <para/>
        /// Default: True
        /// </summary>
        public bool PollAxes
        {
            get;
            set;
        }

        /// <summary>
        /// When polling an axis what directions should be considered. <param/>
        /// Default: <see cref="AxleValueConsideration.Absolute"/>
        /// </summary>
        public AxleValueConsideration AxisConsideration
        {
            get;
            set;
        }

        /// <summary>
        /// When polling an axis what is considered the 'not pressed' dead zone. <para/>
        /// Default: <see cref="InputUtil.DEFAULT_AXLEBTNDEADZONE"/>
        /// </summary>
        public float AxisPollingDeadZone
        {
            get;
            set;
        }

        /// <summary>
        /// If monitoring for button/key presses to emulate an axis this is how long in seconds one should wait before giving up on waiting for the second button press.<para/>
        /// Note - If AxisConsideration is Positive/Negative, it'll register on the first button press.
        /// Default: 5.0f
        /// </summary>
        public float ButtonPressMonitorDuration
        {
            get;
            set;
        }

        /// <summary>
        /// When polling for the axis, consider it as a trigger. In this case it only registers when the value is positive. This can be used to deal with 2 key issues.<para/>
        /// 1) When monitoring for 0->1 presses for triggers, this will allow you to ignore negative presses by monitoring only Positive. <para/>
        /// 2) For strange input devices that register -1 for depressed (PS4 controller on some platforms), this allows you to 
        /// ignore that depressed state by setting it to only monitor Positive. <para/>
        /// Default: True
        /// </summary>
        public bool PollAsTrigger
        {
            get;
            set;
        }

        /// <summary>
        /// Allow the mouse to register as an axis when pulling Standard SPInputs (does not work for profiles). <param/>
        /// Default: False
        /// </summary>
        public bool AllowMouseAsAxis
        {
            get;
            set;
        }

        /// <summary>
        /// A key the user can press to cancel out of the polling. <para/>
        /// Default: <see cref="UnityEngine.KeyCode.Escape"/>
        /// </summary>
        public UnityEngine.KeyCode CancelKey
        {
            get;
            set;
        }

        /// <summary>
        /// A custom button press that can be used to register a cancel instead of the CancelKey. 
        /// Useful for allowing a gamepad button press to cancel with. <para/>
        /// Default: Null
        /// </summary>
        public ButtonDelegate CancelDelegate
        {
            get;
            set;
        }

        /// <summary>
        /// Set this to allow to poll for some custom inputs to register axes.
        /// </summary>
        public AxisPollingCallback CustomAxisPollingCallback
        {
            get;
            set;
        }

        /// <summary>
        /// Set this to allow to poll for some custom inputs to register axes.
        /// </summary>
        public ButtonPollingCallback CustomButtonPollingCallback
        {
            get;
            set;
        }

        #endregion

        #region Methods

        public void Start()
        {
            if (_routine != null)
            {
                if (_routine.Finished)
                {
                    _routine = null;
                    _state = State.Unknown;
                }
                else
                {
                    //already running
                    return;
                }
            }

            _state = State.Running;
            this.DelegateResult = null;
            _routine = GameLoopEntry.Hook.StartRadicalCoroutine(this.WorkRoutine());
        }

        public void Cancel()
        {
            if (_routine != null)
            {
                _routine.Cancel();
                _routine = null;
            }

            _state = State.Cancelled;
            this.DelegateResult = null;
        }

        public IAxleInputSignature CreateInputSignature(string id)
        {
            return new DelegatedAxleInputSignature(id, this.DelegateResult);
        }

        private System.Collections.IEnumerator WorkRoutine()
        {
            ButtonDelegate positive = null;
            float t = float.NegativeInfinity;

            while (_state == State.Running)
            {
                if (UnityEngine.Input.GetKeyDown(this.CancelKey))
                {
                    this.Cancel();
                    yield break;
                }
                if (this.CancelDelegate != null && this.CancelDelegate())
                {
                    this.Cancel();
                    yield break;
                }

                if(this.PollAxes && this.CustomAxisPollingCallback != null)
                {
                    AxisDelegate d;
                    if(this.CustomAxisPollingCallback(this, out d))
                    {
                        this.DelegateResult = d;
                        goto Complete;
                    }
                }

                if(this.PollButtons && this.CustomButtonPollingCallback != null)
                {
                    ButtonDelegate d;
                    if(this.CustomButtonPollingCallback(this, out d))
                    {
                        if (this.PollAsTrigger)
                        {
                            this.DelegateResult = SPInputFactory.CreateAxisDelegate(d, null);
                            goto Complete;
                        }
                        if (positive != null)
                        {
                            this.DelegateResult = SPInputFactory.CreateAxisDelegate(positive, d);
                            goto Complete;
                        }
                        else
                        {
                            positive = d;
                            t = UnityEngine.Time.realtimeSinceStartup;
                            goto Restart;
                        }
                    }
                }

                if (this.PollFromStandardSPInputs)
                {
                    if (this.PollAxes)
                    {
                        SPInputAxis axis;
                        float value;
                        if (SPInputDirect.TryPollAxis(out axis, out value, this.Joystick, this.AxisPollingDeadZone) && TestConsideration(value, this.AxisConsideration, this.AxisPollingDeadZone))
                        {
                            if (this.AllowMouseAsAxis || axis < SPInputAxis.MouseAxis1)
                            {
                                this.DelegateResult = SPInputFactory.CreateAxisDelegate(axis, this.Joystick, value < 0f);
                                goto Complete;
                            }
                        }
                    }

                    if (this.PollButtons)
                    {
                        SPInputButton btn;
                        if (SPInputDirect.TryPollButton(out btn, this.Joystick))
                        {
                            if (this.PollAsTrigger)
                            {
                                var d = SPInputFactory.CreateButtonDelegate(btn, this.Joystick);
                                this.DelegateResult = SPInputFactory.CreateAxisDelegate(d, null);
                                goto Complete;
                            }
                            if (positive != null)
                            {
                                this.DelegateResult = SPInputFactory.CreateAxisDelegate(positive, SPInputFactory.CreateButtonDelegate(btn, this.Joystick));
                                goto Complete;
                            }
                            else
                            {
                                positive = SPInputFactory.CreateButtonDelegate(btn, this.Joystick);
                                t = UnityEngine.Time.realtimeSinceStartup;
                                goto Restart;
                            }
                        }
                    }
                }

                if (this.PollKeyboard)
                {
                    UnityEngine.KeyCode key;
                    if (SPInputDirect.TryPollKey(out key))
                    {
                        if (this.PollAsTrigger)
                        {
                            var d = SPInputFactory.CreateButtonDelegate(key);
                            this.DelegateResult = SPInputFactory.CreateAxisDelegate(d, null);
                            goto Complete;
                        }
                        if (positive != null)
                        {
                            this.DelegateResult = SPInputFactory.CreateAxisDelegate(positive, SPInputFactory.CreateButtonDelegate(key));
                            goto Complete;
                        }
                        else
                        {
                            positive = SPInputFactory.CreateButtonDelegate(key);
                            t = UnityEngine.Time.realtimeSinceStartup;
                            goto Restart;
                        }
                    }
                }

                Restart:
                yield return null;

                if (UnityEngine.Time.realtimeSinceStartup - t > this.ButtonPressMonitorDuration)
                {
                    positive = null;
                }
            }

            Complete:
            _state = State.Complete;
            _routine = null;
        }

        #endregion

        #region IRadicalWaitHandle Interface

        public bool Cancelled
        {
            get { return _state == State.Cancelled; }
        }

        public bool IsComplete
        {
            get { return _state >= State.Cancelled; }
        }

        void IRadicalWaitHandle.OnComplete(Action<IRadicalWaitHandle> callback)
        {

        }

        bool IRadicalYieldInstruction.Tick(out object yieldObject)
        {
            if (_state == State.Running)
            {
                yieldObject = _routine;
                return true;
            }
            else
            {
                yieldObject = null;
                return false;
            }
        }

        #endregion

        #region Static Utils

        private static bool TestConsideration(float value, AxleValueConsideration consideration, float deadZone)
        {
            switch (consideration)
            {
                case AxleValueConsideration.Positive:
                    return value > deadZone;
                case AxleValueConsideration.Negative:
                    return value < -deadZone;
                case AxleValueConsideration.Absolute:
                    return Math.Abs(value) > deadZone;
                default:
                    return false;
            }
        }


        /// <summary>
        /// Create a custom polling callback that polls an array of IInputProfiles. 
        /// This can be useful for input devices that act strange, yet the profile fixes that behaviour.
        /// For example the PS4 controller's L2/R2 buttons are weird and return -1 when depressed, but its profile fixes it.
        /// </summary>
        /// <typeparam name="TButton"></typeparam>
        /// <typeparam name="TAxis"></typeparam>
        /// <param name="profiles"></param>
        /// <returns></returns>
        public static AxisPollingCallback CreateProfileAxisPollingCallback<TButton, TAxis>(params IInputProfile<TButton, TAxis>[] profiles) where TButton : struct, System.IConvertible where TAxis : struct, System.IConvertible
        {
            if (profiles == null || profiles.Length == 0) return null;

            return (PollingAxisSignatureRoutine targ, out AxisDelegate del) =>
            {
                if (targ.PollAxes)
                {
                    foreach (var p in profiles)
                    {
                        TAxis axis;
                        float value;
                        if (p.TryPollAxis(out axis, out value, targ.Joystick, targ.AxisPollingDeadZone) && TestConsideration(value, targ.AxisConsideration, targ.AxisPollingDeadZone))
                        {
                            if (value < 0f)
                            {
                                var d = p.CreateAxisDelegate(axis, targ.Joystick);
                                if (d != null) del = () => -d();
                                else del = null;
                            }
                            else
                                del = p.CreateAxisDelegate(axis, targ.Joystick);

                            return true;
                        }
                    }
                }

                del = null;
                return false;
            };
        }

        /// <summary>
        /// Create a custom polling callback that polls an array of IInputProfiles. 
        /// This can be useful for input devices that act strange, yet the profile fixes that behaviour.
        /// For example the PS4 controller's L2/R2 buttons are weird and return -1 when depressed, but its profile fixes it.
        /// </summary>
        /// <typeparam name="TButton"></typeparam>
        /// <typeparam name="TAxis"></typeparam>
        /// <param name="profiles"></param>
        /// <returns></returns>
        public static ButtonPollingCallback CreateProfileButtonPollingCallback<TButton, TAxis>(params IInputProfile<TButton, TAxis>[] profiles) where TButton : struct, System.IConvertible where TAxis : struct, System.IConvertible
        {
            if (profiles == null || profiles.Length == 0) return null;

            return (PollingAxisSignatureRoutine targ, out ButtonDelegate del) =>
            {
                if(targ.PollButtons)
                {
                    foreach (var p in profiles)
                    {
                        TButton btn;
                        if (p.TryPollButton(out btn, targ.Joystick))
                        {
                            del = p.CreateButtonDelegate(btn, targ.Joystick);
                            return true;
                        }
                    }
                }
                
                del = null;
                return false;
            };
        }

        #endregion

    }

}
