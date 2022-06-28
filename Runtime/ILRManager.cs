//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using EP.U3D.LIBRARY.ASSET;
using EP.U3D.LIBRARY.BASE;
using ILRuntime.Mono.Cecil.Pdb;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AppDomain = ILRuntime.Runtime.Enviorment.AppDomain;

namespace EP.U3D.RUNTIME.ILR
{
    public static class ILRManager
    {
        public static AppDomain AppDomain { get; private set; }
        private static FileStream dllFs = null;
        private static FileStream pdbFs = null;

        public static void Initialize(string path, Action afterInit)
        {
            string dll = !string.IsNullOrEmpty(path) ? path : Constants.LOCAL_ILR_BUNDLE_PATH + "main" + Constants.ILR_BUNDLE_FILE_EXTENSION;
            string pdbPath = dll + ".pdb";
            AppDomain = new AppDomain();
            if (File.Exists(pdbPath))
            {
                dllFs = new FileStream(dll, FileMode.Open, FileAccess.Read);
                pdbFs = new FileStream(pdbPath, FileMode.Open, FileAccess.Read);
                AppDomain.LoadAssembly(dllFs, pdbFs, new PdbReaderProvider());
            }
            else
            {
                dllFs = new FileStream(dll, FileMode.Open, FileAccess.Read);
                AppDomain.LoadAssembly(dllFs);
            }

#if UNITY_EDITOR
            AppDomain.UnityMainThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif
            JSON.JsonMapper.RegisterCLRRedirection(AppDomain);
            AssetManager.BeforeLoadAsset += ILRComponent.BeforeHook;
            AssetManager.AfterLoadAsset += ILRComponent.AfterHook;
            afterInit?.Invoke();
            Helper.Log("ILRuntime has been started.");
        }

        public static void Close()
        {
            AppDomain = null;
            if (dllFs != null)
            {
                dllFs.Close();
                dllFs.Dispose();
            }
            if (pdbFs != null)
            {
                pdbFs.Close();
                pdbFs.Dispose();
            }
            Helper.Log("ILRuntime has been shutdown.");
        }

        private static List<Type> types = null;

        /// <summary>
        /// 获取所有类型
        /// </summary>
        /// <returns></returns>
        public static List<Type> GetTypes()
        {
            if (types == null)
            {
                types = new List<Type>();
                var values = AppDomain.LoadedTypes.Values.ToList();
                foreach (var v in values)
                {
                    types.Add(v.ReflectionType);
                }
            }
            return types;
        }

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <param name="_type"></param>
        /// <returns></returns>
        public static object CreateInstance(Type _type, params object[] args)
        {
            object instance;
            if (_type is ILRuntime.Reflection.ILRuntimeType ilrType)
            {
                instance = ilrType.ILType.Instantiate(args);
            }
            else if (_type is ILRuntime.Reflection.ILRuntimeWrapperType ilrWrapperType)
            {
                instance = Activator.CreateInstance(ilrWrapperType.RealType, args);
            }
            else
            {
                instance = Activator.CreateInstance(_type, args);
            }
            return instance;
        }
    }
}
