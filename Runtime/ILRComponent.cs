//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using UnityEngine;
using LuaInterface;
using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using ILRuntime.Runtime.Intepreter;
using EP.U3D.LIBRARY.BASE;

namespace EP.U3D.RUNTIME.ILR
{
    public abstract class IILRComponent
    {
        [NonSerialized] public GameObject gameObject;
        [NonSerialized] public Transform transform;
        [NonSerialized] public bool enabled;
        public virtual void Awake() { }
        public virtual void OnEnable() { }
        public virtual void Start() { }
        public virtual void OnDisable() { }
        public virtual void Update() { }
        public virtual void LateUpdate() { }
        public virtual void FixedUpdate() { }
        public virtual void OnDestroy() { }
        public virtual void OnTriggerEnter(Collider other) { }
        public virtual void OnTriggerExit(Collider other) { }
        public virtual void OnCollisionEnter(Collision collision) { }
        public virtual void OnCollisionExit(Collision collision) { }
    }

    public class ILRComponent : MonoBehaviour
    {
        [Serializable]
        public class Field
        {
            public string Key;
            public string Type;
            public UnityEngine.Object OValue;
            public byte[] BValue = new byte[16]; // max struct is vector4 with 16 bytes.

            public void Reset()
            {
                OValue = null;
                BValue = new byte[16];
            }
        }

        public string FilePath;
        public string FullName;
        [NonSerialized] public bool Inited;
        [NonSerialized] public bool InitOK;
        [NonSerialized] public IILRComponent Object;
        [NonSerialized] public Type Type;
        public List<Field> Fields = new();

        #region for loadasset hook
        public static Assembly MainDLL; // for debug
        public static Type DType = null; // for dynamic addcomponent
        public static bool frame = false;
        public static readonly List<ILRComponent> comps = new List<ILRComponent>();
        ILRComponent() { if (frame) comps.Add(this); }
        public static void BeforeHook()
        {
            frame = true;
        }

        public static void AfterHook()
        {
            if (comps.Count > 0)
            {
                for (int i = 0; i < comps.Count; i++)
                {
                    ILRComponent comp = comps[i];
                    if (comp && !comp.Inited) comp.Init();
                }
            }
            frame = false;
            comps.Clear();
        }
        #endregion

        public void Init()
        {
            if (Inited) return;
            Inited = true;
            if (DType != null)
            {
                try
                {
                    if (DType != null)
                    {
                        var o = ILRManager.CreateInstance(DType);
                        if (Constants.SCRIPT_BUNDLE_MODE)
                        {
                            Object = (o as ILTypeInstance).CLRInstance as IILRComponent;
                        }
                        else
                        {
                            Object = o as IILRComponent;
                        }
                    }
                    if (Object == null)
                    {
                        Helper.LogError(Constants.RELEASE_MODE ? null : "META-{0}: error caused by nil NEW() ret.", DType.ToString());
                        enabled = false;
                        return;
                    }
                    Type = DType;
                    FullName = Type.FullName;

                }
                catch (Exception e)
                {
                    Helper.LogError(Constants.RELEASE_MODE ? null : "META-{0}: error {1}", DType.ToString(), e);
                    enabled = false;
                    return;
                }
                finally { DType = null; }
            }
            else
            {
                try
                {
                    if (string.IsNullOrEmpty(FullName))
                    {
                        enabled = false;
                        return;
                    }
                    if (Constants.SCRIPT_BUNDLE_MODE)
                    {
                        var types = ILRManager.GetTypes();
                        foreach (var t in types)
                        {
                            if (t.FullName == FullName)
                            {
                                Type = t;
                                break;
                            }
                        }
                    }
                    else
                    {
                        Type = MainDLL?.GetType(FullName);
                    }
                    if (Type != null)
                    {
                        var o = ILRManager.CreateInstance(Type);
                        if (Constants.SCRIPT_BUNDLE_MODE)
                        {
                            Object = (o as ILTypeInstance).CLRInstance as IILRComponent;
                        }
                        else
                        {
                            Object = o as IILRComponent;
                        }
                    }
                    if (Object == null)
                    {
                        Helper.LogError(Constants.RELEASE_MODE ? null : "META-{0}: error caused by nil NEW() ret.", FullName);
                        enabled = false;
                        return;
                    }
                }
                catch (Exception e)
                {
                    Helper.LogError(Constants.RELEASE_MODE ? null : "Init {0} err: {1}", FullName, e);
                    enabled = false;
                    return;
                }
            }
            Object.transform = transform;
            Object.gameObject = gameObject;
            InitOK = true;
        }

        protected virtual void Awake()
        {
            if (!Inited) Init();
            if (!InitOK) return;
            if (Fields != null && Fields.Count > 0)
            {
                for (int i = 0; i < Fields.Count; i++)
                {
                    var field = Fields[i];
                    var ffield = Type.GetField(field.Key);
                    if (ffield == null) continue;
                    if (field.Type == "System.Int32")
                    {
                        int v = BitConverter.ToInt32(field.BValue, 0);
                        ffield.SetValue(Object, v);
                    }
                    else if (field.Type == "System.Int64")
                    {
                        long v = BitConverter.ToInt64(field.BValue, 0);
                        ffield.SetValue(Object, v);
                    }
                    else if (field.Type == "System.Single")
                    {
                        float v = BitConverter.ToSingle(field.BValue, 0);
                        ffield.SetValue(Object, v);
                    }
                    else if (field.Type == "System.Double")
                    {
                        double v = BitConverter.ToDouble(field.BValue, 0);
                        ffield.SetValue(Object, v);
                    }
                    else if (field.Type == "System.Boolean")
                    {
                        bool v = BitConverter.ToBoolean(field.BValue, 0);
                        ffield.SetValue(Object, v);
                    }
                    else if (field.Type == "UnityEngine.Vector2")
                    {
                        Vector2 v = Helper.ByteToStruct<Vector2>(field.BValue);
                        ffield.SetValue(Object, v);
                    }
                    else if (field.Type == "UnityEngine.Vector3")
                    {
                        Vector3 v = Helper.ByteToStruct<Vector3>(field.BValue);
                        ffield.SetValue(Object, v);
                    }
                    else if (field.Type == "UnityEngine.Vector4")
                    {
                        Vector4 v = Helper.ByteToStruct<Vector4>(field.BValue);
                        ffield.SetValue(Object, v);
                    }
                    else if (field.Type == "UnityEngine.Color")
                    {
                        Color v = Helper.ByteToStruct<Color>(field.BValue);
                        ffield.SetValue(Object, v);
                    }
                    else if (field.Type == "System.String")
                    {
                        string v = Encoding.UTF8.GetString(field.BValue);
                        ffield.SetValue(Object, v);
                    }
                    else
                    {
                        if (field.OValue)
                        {
                            if (field.OValue is ILRComponent)
                            {
                                ILRComponent c = field.OValue as ILRComponent;
                                if (c.FullName == field.Type)
                                {
                                    if (!c.Inited) c.Init();
                                    ffield.SetValue(Object, c.Object);
                                }
                            }
                            else
                            {
                                ffield.SetValue(Object, field.OValue);
                            }
                        }
                    }
                }
            }
            if (Application.isPlaying) // release memory
            {
                Fields.Clear();
                Fields = null;
            }
            Object?.Awake();
        }

        protected virtual void Start()
        {
            Object?.Start();
        }

        protected virtual void OnEnable()
        {
            if (Object != null)
            {
                Object.enabled = enabled;
                Object.OnEnable();
            }
        }

        protected virtual void OnDisable()
        {
            if (Object != null)
            {
                Object.enabled = enabled;
                Object.OnDisable();
            }
        }

        protected virtual void Update()
        {
            Object?.Update();
        }

        protected virtual void LateUpdate()
        {
            Object?.LateUpdate();
        }

        protected virtual void FixedUpdate()
        {
            Object?.FixedUpdate();
        }

        protected virtual void OnDestroy()
        {
            Object?.OnDestroy();
            Object = null;
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            Object?.OnTriggerEnter(other);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            Object?.OnTriggerExit(other);
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            Object?.OnCollisionEnter(collision);
        }

        protected virtual void OnCollisionExit(Collision collision)
        {
            Object?.OnCollisionExit(collision);
        }

        public static ILRComponent GetInParent(GameObject go, Type type)
        {
            ILRComponent[] rets = HandleGets(go, type, -1);
            if (rets.Length > 0)
            {
                return rets[0];
            }
            else
            {
                return null;
            }
        }

        public static ILRComponent Get(GameObject go, Type type)
        {
            ILRComponent[] rets = HandleGets(go, type, 0);
            if (rets.Length > 0)
            {
                return rets[0];
            }
            else
            {
                return null;
            }
        }

        public static ILRComponent GetInChildren(GameObject go, Type type, bool includeInactive = false)
        {
            ILRComponent[] rets = HandleGets(go, type, 1, includeInactive);
            if (rets.Length > 0)
            {
                return rets[0];
            }
            else
            {
                return null;
            }
        }

        public static ILRComponent[] GetsInParent(GameObject go, Type type, bool includeInactive = false)
        {
            return HandleGets(go, type, -1, includeInactive);
        }

        public static ILRComponent[] Gets(GameObject go, Type type)
        {
            return HandleGets(go, type, 0);
        }

        public static ILRComponent[] GetsInChildren(GameObject go, Type type, bool includeInactive = false)
        {
            return HandleGets(go, type, 1, includeInactive);
        }

        private static ILRComponent[] HandleGets(GameObject go, Type type, int depth, bool includeInactive = false)
        {
            if (go == null)
            {
                Helper.LogError(Constants.RELEASE_MODE ? null : "error caused by nil gameObject.");
                return null;
            }
            if (type == null)
            {
                Helper.LogError(Constants.RELEASE_MODE ? null : "error caused by nil metatable.");
                return null;
            }
            ILRComponent[] coms;
            if (depth == -1)
            {
                coms = go.GetComponentsInParent<ILRComponent>(includeInactive);
            }
            else if (depth == 0)
            {
                coms = go.GetComponents<ILRComponent>();
            }
            else
            {
                coms = go.GetComponentsInChildren<ILRComponent>(includeInactive);
            }
            List<ILRComponent> rets = new List<ILRComponent>();
            for (int i = 0; i < coms.Length; i++)
            {
                var com = coms[i];
                if (com != null && com.Type != null)
                {
                    Type meta = com.Type;
                    if (type.FullName == meta.FullName)
                    {
                        rets.Add(com);
                    }
                    else
                    {
                        Type pmeta = meta;
                        while (true)
                        {
                            Type bmeta = pmeta.BaseType;
                            if (bmeta == null)
                            {
                                break;
                            }
                            else if (bmeta.FullName == type.FullName)
                            {
                                rets.Add(com);
                                break;
                            }
                            else
                            {
                                pmeta = bmeta;
                            }
                        }
                    }
                }
            }
            return rets.ToArray();
        }
    }
}