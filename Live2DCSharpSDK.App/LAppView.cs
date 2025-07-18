﻿using System.Numerics;

using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Math;

namespace Live2DCSharpSDK.App;

/// <summary>
/// 描画クラス
/// </summary>
/// <remarks>
/// コンストラクタ
/// </remarks>
public abstract class LAppView(LAppDelegate lapp)
{
    private Vector2 _eyesViewpoint;
    private Vector2 _lastMousePosition;
    
    /// <summary>
    /// タッチマネージャー
    /// </summary>
    private readonly TouchManager _touchManager = new();
    /// <summary>
    /// デバイスからスクリーンへの行列
    /// </summary>
    private readonly CubismMatrix44 _deviceToScreen = new();
    /// <summary>
    /// viewMatrix
    /// </summary>
    private readonly CubismViewMatrix _viewMatrix = new();

    /// <summary>
    /// 是
    /// </summary>
    public Vector2 EyesViewpoint
    {
        get => _eyesViewpoint;
        set
        {
            _eyesViewpoint = value;
            // 目の視点を更新
            UpdateEyesViewpointScreen();
        }
    }
    
    public Vector2 EyesViewpointScreen { get; private set; }

    public abstract void RenderPre();
    public abstract void RenderPost();

    /// <summary>
    /// 初期化する。
    /// </summary>
    public void Initialize()
    {
        int width = lapp.WindowWidth;
        int height = lapp.WindowHeight;
        if (width == 0 || height == 0)
        {
            return;
        }

        // 縦サイズを基準とする
        float ratio = (float)width / height;
        float left = -ratio;
        float right = ratio;
        float bottom = LAppDefine.ViewLogicalLeft;
        float top = LAppDefine.ViewLogicalRight;

        _viewMatrix.SetScreenRect(left, right, bottom, top); // デバイスに対応する画面の範囲。 Xの左端, Xの右端, Yの下端, Yの上端
        _viewMatrix.Scale(LAppDefine.ViewScale, LAppDefine.ViewScale);

        _deviceToScreen.LoadIdentity(); // サイズが変わった際などリセット必須
        if (width > height)
        {
            float screenW = MathF.Abs(right - left);
            _deviceToScreen.ScaleRelative(screenW / width, -screenW / width);
        }
        else
        {
            float screenH = MathF.Abs(top - bottom);
            _deviceToScreen.ScaleRelative(screenH / height, -screenH / height);
        }
        _deviceToScreen.TranslateRelative(-width * 0.5f, -height * 0.5f);

        // 表示範囲の設定
        _viewMatrix.MaxScale = LAppDefine.ViewMaxScale; // 限界拡大率
        _viewMatrix.MinScale = LAppDefine.ViewMinScale; // 限界縮小率

        // 表示できる最大範囲
        _viewMatrix.SetMaxScreenRect(
            LAppDefine.ViewLogicalMaxLeft,
            LAppDefine.ViewLogicalMaxRight,
            LAppDefine.ViewLogicalMaxBottom,
            LAppDefine.ViewLogicalMaxTop
        );
    }

    /// <summary>
    /// 描画する。
    /// </summary>
    internal void Render()
    {
        RenderPre();

        var manager = lapp.Live2dManager;
        manager.ViewMatrix.SetMatrix(_viewMatrix);

        // Cubism更新・描画
        manager.OnUpdate();

        RenderPost();
    }

    /// <summary>
    /// タッチされたときに呼ばれる。
    /// </summary>
    /// <param name="pointX">スクリーンX座標</param>
    /// <param name="pointY">スクリーンY座標</param>
    public void OnTouchesBegan(float pointX, float pointY)
    {
        _lastMousePosition = new Vector2(pointX, pointY);
        _touchManager.TouchesBegan(pointX - lapp.WindowX, pointY - lapp.WindowY);
        CubismLog.Debug($"[Live2D App]touchesBegan x:{pointX:#.##} y:{pointY:#.##}");
    }

    /// <summary>
    /// タッチしているときにポインタが動いたら呼ばれる。
    /// </summary>
    /// <param name="pointX">スクリーンX座標</param>
    /// <param name="pointY">スクリーンY座標</param>
    public void OnTouchesMoved(float pointX, float pointY)
    {
        var nowPos = new Vector2(pointX, pointY);
        var delta = nowPos - _lastMousePosition;
        _lastMousePosition = nowPos;
        
        var newPos = new Vector2(lapp.WindowX, lapp.WindowY) + delta;
        lapp.WindowsPositionSetter?.Invoke(newPos);

        _touchManager.TouchesMoved(pointX - lapp.WindowX, pointY - lapp.WindowY);

        // lapp.Live2dManager.OnDrag(viewX, viewY);
    }

    public void OnLookingMoved(float pointX, float pointY)
    {
        pointY = lapp.MonitorHeight - pointY;
        // Console.WriteLine($"OnLookingMoved: pointX={pointX}, pointY={pointY}");
        if (pointX < EyesViewpointScreen.X)
            pointX = (pointX - EyesViewpointScreen.X) / EyesViewpointScreen.X;
        else
            pointX = (pointX - EyesViewpointScreen.X) / (lapp.MonitorWidth - EyesViewpointScreen.X);
        
        if (pointY < EyesViewpointScreen.Y)
            pointY = (pointY - EyesViewpointScreen.Y) / EyesViewpointScreen.Y;
        else
            pointY = (pointY - EyesViewpointScreen.Y) / (lapp.MonitorHeight - EyesViewpointScreen.Y);
        
        lapp.Live2dManager.OnDrag(pointX, pointY);
    }

    /// <summary>
    /// タッチが終了したら呼ばれる。
    /// </summary>
    /// <param name="pointX">スクリーンX座標</param>
    /// <param name="pointY">スクリーンY座標</param>
    public void OnTouchesEnded(float _, float __)
    {
        // タッチ終了
        var live2DManager = lapp.Live2dManager;
        // live2DManager.OnDrag(0.0f, 0.0f);
        // シングルタップ
        float x = _deviceToScreen.TransformX(_touchManager.GetX()); // 論理座標変換した座標を取得。
        float y = _deviceToScreen.TransformY(_touchManager.GetY()); // 論理座標変換した座標を取得。
        CubismLog.Debug($"[Live2D App]touchesEnded x:{x:#.##} y:{y:#.##}");
        //live2DManager.OnTap(x, y);
    }

    public void UpdateWindowPosition(int x, int y)
    {
        UpdateEyesViewpointScreen();
    }
    
    private void UpdateEyesViewpointScreen()
    {                        
        var screen = new Vector2(
            _deviceToScreen.InvertTransformX(_viewMatrix.InvertTransformX(_eyesViewpoint.X)) + lapp.WindowX,
            _deviceToScreen.InvertTransformY(_viewMatrix.InvertTransformY(_eyesViewpoint.Y)) + (lapp.MonitorHeight - lapp.WindowHeight - lapp.WindowY)
        );
        if (screen.X == 0) screen.X = 1;
        if (screen.Y == 0) screen.Y = 1;
        EyesViewpointScreen = screen;
        Console.WriteLine($"Update eyes viewpoint: {_eyesViewpoint}, screen: {EyesViewpointScreen}");
    }

    /// <summary>
    /// X座標をView座標に変換する。
    /// </summary>
    /// <param name="deviceX">デバイスX座標</param>
    public float TransformViewX(float deviceX)
    {
        float screenX = _deviceToScreen.TransformX(deviceX); // 論理座標変換した座標を取得。
        return _viewMatrix.InvertTransformX(screenX); // 拡大、縮小、移動後の値。
    }

    /// <summary>
    /// Y座標をView座標に変換する。
    /// </summary>
    /// <param name="deviceY">デバイスY座標</param>
    public float TransformViewY(float deviceY)
    {
        float screenY = _deviceToScreen.TransformY(deviceY); // 論理座標変換した座標を取得。
        return _viewMatrix.InvertTransformY(screenY); // 拡大、縮小、移動後の値。
    }

    /// <summary>
    /// X座標をScreen座標に変換する。
    /// </summary>
    /// <param name="deviceX">デバイスX座標</param>
    public float TransformScreenX(float deviceX)
    {
        return _deviceToScreen.TransformX(deviceX);
    }

    /// <summary>
    /// Y座標をScreen座標に変換する。
    /// </summary>
    /// <param name="deviceY">デバイスY座標</param>
    public float TransformScreenY(float deviceY)
    {
        return _deviceToScreen.TransformY(deviceY);
    }

    /// <summary>
    /// 別レンダリングターゲットにモデルを描画するサンプルで
    /// 描画時のαを決定する
    /// </summary>
    public static float GetSpriteAlpha(int assign)
    {
        // assignの数値に応じて適当に決定
        float alpha = 0.25f + assign * 0.5f; // サンプルとしてαに適当な差をつける
        if (alpha > 1.0f)
        {
            alpha = 1.0f;
        }
        if (alpha < 0.1f)
        {
            alpha = 0.1f;
        }

        return alpha;
    }
}
