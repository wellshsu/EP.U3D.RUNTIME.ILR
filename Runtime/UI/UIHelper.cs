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
using EP.U3D.LIBRARY.BASE;

namespace EP.U3D.RUNTIME.ILR.UI
{
    public class UIHelper : LIBRARY.UI.UIHelper
    {
        public static System.Type ClazzUnityObject = typeof(Object);

        public static new object GetComponentInParent(Object rootObj, System.Type type)
        {
            return GetComponentInParent(rootObj, null, type);
        }

        public static new object GetComponentInParent(Object parentObj, string path, System.Type type)
        {
            if (type == null)
            {
                Helper.LogError("missing type argument");
                return null;
            }
            Transform root = GetTransform(parentObj, path);
            if (root && root.gameObject)
            {
                if (type.IsSubclassOf(ClazzUnityObject))
                {
                    return root.gameObject.GetComponentInParent(type);
                }
                else
                {
                    ILRComponent comp = ILRComponent.GetInParent(root.gameObject, type);
                    if (comp)
                    {
                        return comp.Object;
                    }
                }
            }
            return null;
        }

        public static new object GetComponent(Object rootObj, System.Type type)
        {
            return GetComponent(rootObj, null, type);
        }

        public static new object GetComponent(Object parentObj, string path, System.Type type)
        {
            if (type == null)
            {
                Helper.LogError("missing type argument");
                return null;
            }
            Transform root = GetTransform(parentObj, path);
            if (root && root.gameObject)
            {
                if (type.IsSubclassOf(ClazzUnityObject))
                {
                    return root.gameObject.GetComponent(type);
                }
                else
                {
                    ILRComponent comp = ILRComponent.Get(root.gameObject, type);
                    if (comp)
                    {
                        return comp.Object;
                    }
                }
            }
            return null;
        }

        public static new object GetComponentInChildren(Object rootObj, System.Type type, bool includeInactive = false)
        {
            return GetComponentInChildren(rootObj, null, type, includeInactive);
        }

        public static new object GetComponentInChildren(Object parentObj, string path, System.Type type, bool includeInactive = false)
        {
            if (type == null)
            {
                Helper.LogError("missing type argument");
                return null;
            }
            Transform root = GetTransform(parentObj, path);
            if (root && root.gameObject)
            {
                if (type.IsSubclassOf(ClazzUnityObject))
                {
                    return root.gameObject.GetComponentInChildren(type, includeInactive);
                }
                else
                {
                    ILRComponent comp = ILRComponent.GetInChildren(root.gameObject, type, includeInactive);
                    if (comp)
                    {
                        return comp.Object;
                    }
                }
            }
            return null;
        }

        public static new object[] GetComponentsInParent(Object rootObj, System.Type type, bool includeInactive = false)
        {
            return GetComponentsInParent(rootObj, null, type, includeInactive);
        }

        public static new object[] GetComponentsInParent(Object parentObj, string path, System.Type type, bool includeInactive = false)
        {
            if (type == null)
            {
                Helper.LogError("missing type argument");
                return null;
            }
            System.Array arr;
            Transform root = GetTransform(parentObj, path);
            if (type.IsSubclassOf(ClazzUnityObject))
            {
                if (root && root.gameObject)
                {
                    var comps = root.gameObject.GetComponentsInParent(type, includeInactive);
                    arr = System.Array.CreateInstance(type, comps.Length);
                    for (int i = 0; i < comps.Length; i++)
                    {
                        arr.SetValue(comps[i], i);
                    }
                }
                else
                {
                    arr = System.Array.CreateInstance(type, 0);
                }
            }
            else
            {
                if (root && root.gameObject)
                {
                    ILRComponent[] comps = ILRComponent.GetsInParent(root.gameObject, type, includeInactive);
                    if (type is ILRuntime.Reflection.ILRuntimeType itype)
                    {
                        arr = System.Array.CreateInstance(itype.ILType.TypeForCLR, comps.Length);
                    }
                    else
                    {
                        arr = System.Array.CreateInstance(type, comps.Length);
                    }
                    for (int i = 0; i < comps.Length; i++)
                    {
                        arr.SetValue(comps[i].Object, i);
                    }
                }
                else
                {
                    if (type is ILRuntime.Reflection.ILRuntimeType itype)
                    {
                        arr = System.Array.CreateInstance(itype.ILType.TypeForCLR, 0);
                    }
                    else
                    {
                        arr = System.Array.CreateInstance(type, 0);
                    }
                }
            }
            return arr as object[];
        }

        public static new object[] GetComponents(Object rootObj, System.Type type)
        {
            return GetComponents(rootObj, null, type);
        }


        public static new object[] GetComponents(Object parentObj, string path, System.Type type)
        {
            if (type == null)
            {
                Helper.LogError("missing type argument");
                return null;
            }
            System.Array arr;
            Transform root = GetTransform(parentObj, path);
            if (type.IsSubclassOf(ClazzUnityObject))
            {
                if (root && root.gameObject)
                {
                    var comps = root.gameObject.GetComponents(type);
                    arr = System.Array.CreateInstance(type, comps.Length);
                    for (int i = 0; i < comps.Length; i++)
                    {
                        arr.SetValue(comps[i], i);
                    }
                }
                else
                {
                    arr = System.Array.CreateInstance(type, 0);
                }
            }
            else
            {
                if (root && root.gameObject)
                {
                    ILRComponent[] comps = ILRComponent.Gets(root.gameObject, type);
                    if (type is ILRuntime.Reflection.ILRuntimeType itype)
                    {
                        arr = System.Array.CreateInstance(itype.ILType.TypeForCLR, comps.Length);
                    }
                    else
                    {
                        arr = System.Array.CreateInstance(type, comps.Length);
                    }
                    for (int i = 0; i < comps.Length; i++)
                    {
                        arr.SetValue(comps[i].Object, i);
                    }
                }
                else
                {
                    if (type is ILRuntime.Reflection.ILRuntimeType itype)
                    {
                        arr = System.Array.CreateInstance(itype.ILType.TypeForCLR, 0);
                    }
                    else
                    {
                        arr = System.Array.CreateInstance(type, 0);
                    }
                }
            }
            return arr as object[];
        }

        public static new object[] GetComponentsInChildren(Object rootObj, System.Type type, bool includeInactive = false)
        {
            return GetComponentsInChildren(rootObj, null, type, includeInactive);
        }

        public static new object[] GetComponentsInChildren(Object parentObj, string path, System.Type type, bool includeInactive = false)
        {
            if (type == null)
            {
                Helper.LogError("missing type argument");
                return null;
            }
            System.Array arr;
            Transform root = GetTransform(parentObj, path);
            if (type.IsSubclassOf(ClazzUnityObject))
            {
                if (root && root.gameObject)
                {
                    var comps = root.gameObject.GetComponentsInChildren(type, includeInactive);
                    arr = System.Array.CreateInstance(type, comps.Length);
                    for (int i = 0; i < comps.Length; i++)
                    {
                        arr.SetValue(comps[i], i);
                    }
                }
                else
                {
                    arr = System.Array.CreateInstance(type, 0);
                }
            }
            else
            {
                if (root && root.gameObject)
                {
                    ILRComponent[] comps = ILRComponent.GetsInChildren(root.gameObject, type, includeInactive);
                    if (type is ILRuntime.Reflection.ILRuntimeType itype)
                    {
                        arr = System.Array.CreateInstance(itype.ILType.TypeForCLR, comps.Length);
                    }
                    else
                    {
                        arr = System.Array.CreateInstance(type, comps.Length);
                    }
                    for (int i = 0; i < comps.Length; i++)
                    {
                        arr.SetValue(comps[i].Object, i);
                    }
                }
                else
                {
                    if (type is ILRuntime.Reflection.ILRuntimeType itype)
                    {
                        arr = System.Array.CreateInstance(itype.ILType.TypeForCLR, 0);
                    }
                    else
                    {
                        arr = System.Array.CreateInstance(type, 0);
                    }
                }
            }
            return arr as object[];
        }

        public static new object AddComponent(Object rootObj, System.Type type)
        {
            return AddComponent(rootObj, null, type);
        }

        public static new object AddComponent(Object parentObj, string path, System.Type type)
        {
            if (type == null)
            {
                Helper.LogError("missing type argument");
                return null;
            }
            Transform root = GetTransform(parentObj, path);
            if (root && root.gameObject)
            {
                if (type.IsSubclassOf(ClazzUnityObject))
                {
                    return root.gameObject.AddComponent(type);
                }
                else
                {
                    ILRComponent.DType = type;
                    ILRComponent comp = root.gameObject.AddComponent<ILRComponent>();
                    return comp.Object;
                }
            }
            else
            {
                return null;
            }
        }

        public static new void RemoveComponent(Object rootObj, System.Type type)
        {
            RemoveComponent(rootObj, null, type, false);
        }

        public static new void RemoveComponent(Object parentObj, string path, System.Type type, bool immediate)
        {
            if (type == null)
            {
                Helper.LogError("missing type argument");
                return;
            }
            if (type.IsSubclassOf(ClazzUnityObject))
            {
                Object obj = GetComponent(parentObj, path, type) as Object;
                if (obj)
                {
                    if (immediate)
                    {
                        Object.DestroyImmediate(obj);
                    }
                    else
                    {
                        Object.Destroy(obj);
                    }
                }
            }
            else
            {
                Transform root = GetTransform(parentObj, path);
                if (root && root.gameObject)
                {
                    ILRComponent comp = ILRComponent.Get(root.gameObject, type);
                    if (comp)
                    {
                        if (immediate)
                        {
                            Object.DestroyImmediate(comp);
                        }
                        else
                        {
                            Object.Destroy(comp);
                        }
                    }
                }
            }
        }

        public static new object SetComponentEnabled(Object rootObj, System.Type type, bool enabled)
        {
            return SetComponentEnabled(rootObj, null, type, enabled);
        }

        public static new object SetComponentEnabled(Object parentObj, string path, System.Type type, bool enabled)
        {
            if (type == null)
            {
                Helper.LogError("missing type argument");
                return null;
            }
            if (type.IsSubclassOf(ClazzUnityObject))
            {
                Behaviour behaviour = GetComponent(parentObj, path, type) as Behaviour;
                if (behaviour)
                {
                    behaviour.enabled = enabled;
                }
                return behaviour;
            }
            else
            {
                Transform root = GetTransform(parentObj, path);
                if (root && root.gameObject)
                {
                    ILRComponent comp = ILRComponent.Get(root.gameObject, type);
                    if (comp)
                    {
                        comp.enabled = enabled;
                        return comp.Object;
                    }
                }
            }
            return null;
        }
    }
}