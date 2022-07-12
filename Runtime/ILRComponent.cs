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
using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using ILRuntime.Runtime.Intepreter;
using EP.U3D.LIBRARY.BASE;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        public virtual void OnTriggerStay(Collider other) { }
        public virtual void OnTriggerExit(Collider other) { }
        public virtual void OnCollisionEnter(Collision collision) { }
        public virtual void OnCollisionExit(Collision collision) { }
    }

    public class ILRComponent : MonoBehaviour
    {
        [Serializable]
        public class Byte
        {
            public byte[] Data;
            public Byte(byte[] data = null) { Data = data; }
        }

        [Serializable]
        public class Field
        {
            public string Key;
            public string Type;
            public UnityEngine.Object OValue;
            public byte[] BValue = new byte[16]; // max struct is vector4 with 16 bytes.
            public List<UnityEngine.Object> LOValue; // list of OValue
            public List<Byte> LBValue; // list of BValue
            public bool BTArray = false; // is array type
            public bool BTList = false; // is list type
            public bool BLBValue = false; // is use LBValue for list
            [NonSerialized] public bool BLShow = false; // is show list

            public void Reset()
            {
                Type = "";
                OValue = null;
                BValue = new byte[16];
                LOValue = null;
                LBValue = null;
                BTArray = false;
                BTList = false;
                BLBValue = false;
                BLShow = false;
            }
        }

        public string FilePath;
        public string FullName;
        [NonSerialized] public bool Inited;
        [NonSerialized] public bool InitOK;
        [NonSerialized] public IILRComponent Object;
        [NonSerialized] public Type Type;
        public List<Field> Fields = new List<Field>();

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

#if UNITY_EDITOR
        private void sceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= sceneLoaded;
            var c = comps.Count;
            AfterHook();
            Helper.Log("after scene loaded and inited {0} depend ilrcomponents.", c);
        }
#endif

        public void Init()
        {
#if UNITY_EDITOR
            // [20220710]: single debug
            if (ILRManager.AppDomain == null)
            {
                if (frame == false)
                {
                    BeforeHook();
                    SceneManager.sceneLoaded += sceneLoaded;
                }
                if (MainDLL == null)
                {
                    var types = UnityEditor.TypeCache.GetTypesDerivedFrom<IILRComponent>();
                    for (int i = 0; i < types.Count; i++)
                    {
                        var type = types[i];
                        var dll = Assembly.GetAssembly(type);
                        if (dll != null && dll.FullName.Contains("Assembly-CSharp"))
                        {
                            MainDLL = dll;
                            break;
                        }
                    }
                    if (MainDLL == null) Helper.LogError("missing dll for ilrcomponent debug");
                    else Helper.Log("using [{0}] for ilrcomponent debug", MainDLL.FullName);
                }
            }
#endif
            if (Inited) return;
            Inited = true;
            if (DType != null)
            {
                try
                {
                    if (DType != null)
                    {
                        var o = ILRManager.CreateInstance(DType);
                        if (Constants.SCRIPT_BUNDLE_MODE && ILRManager.AppDomain != null)
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
                    if (Constants.SCRIPT_BUNDLE_MODE && ILRManager.AppDomain != null)
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
                        if (Constants.SCRIPT_BUNDLE_MODE && ILRManager.AppDomain != null)
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
            if (Fields != null && Fields.Count > 0)
            {
                for (int i = 0; i < Fields.Count; i++)
                {
                    var field = Fields[i];
                    var ffield = Type.GetField(field.Key);
                    if (ffield == null) continue;
                    if (field.BTArray || field.BTList)
                    {
                        var length = field.BLBValue ? field.LBValue.Count : field.LOValue.Count;
                        if (field.BTArray)
                        {
                            Array arr;
                            var type = ffield.FieldType.GetElementType();
                            if (type is ILRuntime.Reflection.ILRuntimeType itype)
                            {
                                arr = Array.CreateInstance(itype.ILType.TypeForCLR, length);
                            }
                            else
                            {
                                arr = Array.CreateInstance(type, length);
                            }
                            if (field.BLBValue)
                            {
                                for (int j = 0; j < field.LBValue.Count; j++)
                                {
                                    SetField(field.Key, field.Type, type, field.LBValue[j].Data, null, out object fvalue);
                                    arr.SetValue(fvalue, j);
                                }
                            }
                            else
                            {
                                for (int j = 0; j < field.LOValue.Count; j++)
                                {
                                    UnityEngine.Object ovalue = field.LOValue[j];
                                    if (ovalue)
                                    {
                                        SetField(field.Key, field.Type, type, null, ovalue, out object fvalue);
                                        arr.SetValue(fvalue, j);
                                    }
                                }
                            }
                            ffield.SetValue(Object, arr);
                        }
                        else
                        {
                            Type ltype = typeof(List<>);
                            object list;
                            var type = ffield.FieldType.GetGenericArguments()[0];
                            if (type is ILRuntime.Reflection.ILRuntimeType itype)
                            {
                                Type ntype = ltype.MakeGenericType(new Type[] { itype.ILType.TypeForCLR });
                                list = Activator.CreateInstance(ntype);
                            }
                            else
                            {
                                Type ntype = ltype.MakeGenericType(new Type[] { type });
                                list = Activator.CreateInstance(ntype);
                            }
                            MethodInfo add = list.GetType().GetMethod("Add");
                            if (field.BLBValue)
                            {
                                for (int j = 0; j < field.LBValue.Count; j++)
                                {
                                    SetField(field.Key, field.Type, type, field.LBValue[j].Data, null, out object fvalue);
                                    add.Invoke(list, new object[] { fvalue });
                                }
                            }
                            else
                            {
                                for (int j = 0; j < field.LOValue.Count; j++)
                                {
                                    SetField(field.Key, field.Type, type, null, field.LOValue[j], out object fvalue);
                                    add.Invoke(list, new object[] { fvalue });
                                }
                            }
                            ffield.SetValue(Object, list);
                        }
                    }
                    else
                    {
                        SetField(field.Key, field.Type, ffield.FieldType, field.BValue, field.OValue, out object fvalue);
                        ffield.SetValue(Object, fvalue);
                    }
                }
            }
            if (Application.isPlaying) // release memory
            {
                Fields.Clear();
                Fields = null;
            }
            InitOK = true;
        }

        private void SetField(string key, string stype, Type type, byte[] bvalue, UnityEngine.Object ovalue, out object fvalue)
        {
            fvalue = null;
            if (stype == "System.Int32")
            {
                fvalue = BitConverter.ToInt32(bvalue, 0);
            }
            else if (stype == "System.Int64")
            {
                fvalue = BitConverter.ToInt64(bvalue, 0);
            }
            else if (stype == "System.Single")
            {
                fvalue = BitConverter.ToSingle(bvalue, 0);
            }
            else if (stype == "System.Double")
            {
                fvalue = BitConverter.ToDouble(bvalue, 0);
            }
            else if (stype == "System.Boolean")
            {
                fvalue = BitConverter.ToBoolean(bvalue, 0);
            }
            else if (stype == "UnityEngine.Vector2")
            {
                fvalue = Helper.ByteToStruct<Vector2>(bvalue);
            }
            else if (stype == "UnityEngine.Vector3")
            {
                fvalue = Helper.ByteToStruct<Vector3>(bvalue);
            }
            else if (stype == "UnityEngine.Vector4")
            {
                fvalue = Helper.ByteToStruct<Vector4>(bvalue);
            }
            else if (stype == "UnityEngine.Color")
            {
                fvalue = Helper.ByteToStruct<Color>(bvalue);
            }
            else if (stype == "System.String")
            {
                fvalue = Encoding.UTF8.GetString(bvalue);
            }
            else
            {
                if (ovalue)
                {
                    if (ovalue is ILRComponent)
                    {
                        ILRComponent c = ovalue as ILRComponent;
                        if (c.FullName == stype)
                        {
                            if (!c.Inited) c.Init();
                            fvalue = c.Object;
                        }
                    }
                    else
                    {
                        fvalue = ovalue;
                    }
                }
                else
                {
                    if (type.IsEnum)
                    {
                        fvalue = BitConverter.ToInt32(bvalue, 0);
                    }
                }
            }
            if (fvalue == null) Helper.LogWarning("parse {0}.{1}({2}) of component {3} error", FullName, key, stype, name);
        }

        protected virtual void Awake()
        {
            if (!Inited) Init();
            if (!InitOK) return;
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

        protected virtual void OnTriggerStay(Collider other)
        {
            Object?.OnTriggerStay(other);
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
                if (com && !com.Inited) com.Init();
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