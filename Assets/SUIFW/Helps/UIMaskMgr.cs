// UI遮罩管理器      
// 功能： 负责“弹出窗体”的模态实现。

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;


namespace SUIFW
{
    public class UIMaskMgr : MonoBehaviour
    {
        /* 字段 */
        //本脚本私有单例实例
        private static UIMaskMgr _Instance;                 
        //UI根节点对象
        private GameObject _GoCanvasRoot = null;
        //UI脚本节点（加载各种管理脚本的节点）
        private Transform _CanTransformUIScripts = null;
        //顶层面板
        private GameObject _GoTopPlane;                     
        //遮罩面板
        private GameObject _GoMaskPlane;                    
        //UI摄像机
        private Camera _UICamear;                           
        //原始UI摄像机的层深
        private float _OriginalUICameraDepth;              


        /// <summary>
        /// 得到实例(单例)
        /// </summary>
        /// <returns></returns>
        public static UIMaskMgr GetInstance()
        {
            if (_Instance == null)
            {
                var canvasRoot = GameObject.FindGameObjectWithTag(SysDefine.SYS_TAG_CANVAS);
                _Instance = canvasRoot != null
                    ? canvasRoot.GetComponentInChildren<UIMaskMgr>(true)
                    : null;
                if (_Instance == null)
                {
                    Debug.LogError("[UIMaskMgr] Canvas 的 _UIScripts 节点缺少显式 UIMaskMgr 组件。");
                }
            }
            return _Instance;
        }

        void Awake()
        {
            _Instance = this;

            //得到UI根节点、UI脚本节点                    
            _GoCanvasRoot = GameObject.FindGameObjectWithTag(SysDefine.SYS_TAG_CANVAS);
            if (_GoCanvasRoot == null)
            {
                Debug.LogError("[UIMaskMgr] 找不到带 _TagCanvas 标签的 UI 根节点。", this);
                return;
            }

            _CanTransformUIScripts = UnityHelper.FindTheChild(_GoCanvasRoot, SysDefine.SYS_CANVAS_UISCRIPTS_NODE_NAME);
            //把本脚本实例，作为Canvas的子节点
            if (_CanTransformUIScripts != null && _CanTransformUIScripts != transform)
            {
                UnityHelper.AddChildToParent(_CanTransformUIScripts, this.gameObject.transform);
            }

            //得到“顶层面板”与“遮罩面板”
            _GoTopPlane = _GoCanvasRoot;
            var maskTransform = UnityHelper.FindTheChild(
                _GoCanvasRoot.gameObject,
                SysDefine.SYS_CANVAS_UIMASKPANELS_NODE_NAME);
            _GoMaskPlane = maskTransform != null ? maskTransform.gameObject : null;
            if (_GoMaskPlane == null)
            {
                Debug.LogError("[UIMaskMgr] Canvas 缺少 UIMaskPanels 节点。", this);
            }

            //得到UI摄像机的原始“层深”
            // Screen Space Overlay 不需要 UI Camera；SnowValley 正式 HUD 使用该模式，
            // 且 Canvas 中保留的兼容 UICamera 默认停用，FindGameObjectWithTag 找不到它。
            var uiCameraObject = GameObject.FindGameObjectWithTag(SysDefine.SYS_TAG_UICAMERA);
            _UICamear = uiCameraObject != null
                ? uiCameraObject.GetComponent<Camera>()
                : _GoCanvasRoot.GetComponentInChildren<Camera>(true);
            if (_UICamear != null)
            {
                _OriginalUICameraDepth = _UICamear.depth;
            }
        }

        /// <summary>
        /// 设置遮罩状态
        /// </summary>
        /// <param name="goDisplayPlane">需要显示的窗体</param>
        public void SetMaskWindow(GameObject goDisplayPlane,UIFormsLucencyType UILucencyType=UIFormsLucencyType.Lucency)
        {
            if (goDisplayPlane == null)
            {
                return;
            }

            //顶层窗体下移。
            _GoTopPlane?.transform.SetAsLastSibling();

            //启用遮罩窗体与透明度
            if (_GoMaskPlane != null)
            {
                switch (UILucencyType)
                {
                    case UIFormsLucencyType.Lucency:
                        _GoMaskPlane.SetActive(true);
                        // Lucency blocks input behind the popup without visually
                        // dimming the scene. Activity forms own their own framing.
                        Color newColor1 = new Color(0f, 0f, 0f, 0f);
                        SetMaskColor(newColor1);
                        break;
                    case UIFormsLucencyType.Translucence:
                        _GoMaskPlane.SetActive(true);
                        Color newColor2 = new Color(SysDefine.SYS_UIMASK_TRANSLUCENCY_COLOR_RGB, SysDefine.SYS_UIMASK_TRANSLUCENCY_COLOR_RGB, SysDefine.SYS_UIMASK_TRANSLUCENCY_COLOR_RGB, SysDefine.SYS_UIMASK_TRANSLUCENCY_COLOR_A);
                        SetMaskColor(newColor2);
                        break;
                    case UIFormsLucencyType.Impenetrable:
                        _GoMaskPlane.SetActive(true);
                        Color newColor3 = new Color(SysDefine.SYS_UIMASK_IMPENETRABLE_COLOR_RGB, SysDefine.SYS_UIMASK_IMPENETRABLE_COLOR_RGB, SysDefine.SYS_UIMASK_IMPENETRABLE_COLOR_RGB, SysDefine.SYS_UIMASK_IMPENETRABLE_COLOR_A);
                        SetMaskColor(newColor3);
                        break;
                    case UIFormsLucencyType.Penetrate:
                        if (_GoMaskPlane.activeInHierarchy)
                        {
                            _GoMaskPlane.SetActive(false);
                        }
                        break;
                }

                //遮罩窗体下移
                _GoMaskPlane.transform.SetAsLastSibling();
            }

            //显示窗体下移
            goDisplayPlane.transform.SetAsLastSibling();
            //增加当前UI摄像机的“层深”
            if (_UICamear != null)
            {
                _UICamear.depth = _UICamear.depth + SysDefine.SYS_UICAMERA_DEPTH_INCREMENT;
            }
        }

        /// <summary>
        /// 取消遮罩窗体
        /// </summary>
        public void CancleMaskWindow()
        {
            //顶层窗体上移
            _GoTopPlane?.transform.SetAsFirstSibling();
            //禁用遮罩窗体
            if (_GoMaskPlane != null && _GoMaskPlane.activeInHierarchy)
            {
                _GoMaskPlane.SetActive(false);
            }
            //回复UI摄像机的原来的“层深”
            if (_UICamear != null)
            {
                _UICamear.depth = _OriginalUICameraDepth;
            }
        }	

        private void SetMaskColor(Color color)
        {
            if (_GoMaskPlane != null && _GoMaskPlane.TryGetComponent<Image>(out var image))
            {
                image.color = color;
            }
        }

        private void OnDestroy()
        {
            if (_Instance == this)
            {
                _Instance = null;
            }
        }
    }//Class_end
}
