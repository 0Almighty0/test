using System;
using System.Collections.Generic;
using System.Diagnostics;
using CUE4Parse.UE4.Objects.Core.Misc;
using FModel.Framework;
using ImGuiNET;
using OpenTK.Windowing.Common;
using System.Numerics;
using System.Text;
using FModel.Settings;
using FModel.Views.Snooper.Animations;
using FModel.Views.Snooper.Models;
using FModel.Views.Snooper.Shading;
using ImGuizmoNET;
using OpenTK.Graphics.OpenGL4;

namespace FModel.Views.Snooper;

public class Swap
{
    public string Title;
    public string Description;
    public bool Value;
    public bool IsAware;
    public Action Content;

    public Swap()
    {
        Reset();
    }

    public void Reset()
    {
        Title = string.Empty;
        Description = string.Empty;
        Value = false;
        Content = null;
    }
}

public class Save
{
    public bool Value;
    public string Label;
    public string Path;

    public Save()
    {
        Reset();
    }

    public void Reset()
    {
        Value = false;
        Label = string.Empty;
        Path = string.Empty;
    }
}

public class SnimGui
{
    public readonly ImGuiController Controller;
    private readonly Swap _swapper = new ();
    private readonly Save _saver = new ();
    private readonly string _renderer;
    private readonly string _version;
    private readonly float _tableWidth;

    private Vector2 _outlinerSize;
    private bool _tiOpen;
    private bool _transformOpen;
    private bool _viewportFocus;
    private OPERATION _guizmoOperation;

    private readonly Vector4 _accentColor = new (0.125f, 0.42f, 0.831f, 1.0f);
    private readonly Vector4 _alertColor = new (0.831f, 0.573f, 0.125f, 1.0f);
    private readonly Vector4 _errorColor = new (0.761f, 0.169f, 0.169f, 1.0f);

    private const uint _dockspaceId = 1337;

    public SnimGui(int width, int height)
    {
        Controller = new ImGuiController(width, height);

        _renderer = GL.GetString(StringName.Renderer);
        _version = "OpenGL " + GL.GetString(StringName.Version);
        _tableWidth = 17 * Controller.DpiScale;
        _guizmoOperation = OPERATION.TRANSLATE;

        Theme();
    }

    public void Render(Snooper s)
    {
        ImGui.DockSpaceOverViewport(_dockspaceId, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

        SectionWindow("材质检查器", s.Renderer, DrawMaterialInspector, false);
        AnimationWindow("时间轴", s.Renderer, (icons, tracker, animations) =>
            tracker.ImGuiTimeline(s, _saver, icons, animations, _outlinerSize, Controller.FontSemiBold));

        Window("场景", () => DrawWorld(s), false);

        DrawSockets(s);
        DrawOuliner(s);
        DrawDetails(s);
        Draw3DViewport(s);
        DrawNavbar();

        DrawTextureInspector(s);
        DrawSkeletonTree(s);

        DrawModals(s);

        Controller.Render();
    }

    private void DrawModals(Snooper s)
    {
        Modal(_swapper.Title, _swapper.Value, () =>
        {
            ImGui.TextWrapped(_swapper.Description);
            ImGui.Separator();

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            ImGui.Checkbox("知道了，不再显示", ref _swapper.IsAware);
            ImGui.PopStyleVar();

            var size = new Vector2(120, 0);
            if (ImGui.Button("确定", size))
            {
                _swapper.Content();
                _swapper.Reset();
                ImGui.CloseCurrentPopup();
                s.WindowShouldClose(true, false);
            }

            ImGui.SetItemDefaultFocus();
            ImGui.SameLine();

            if (ImGui.Button("取消", size))
            {
                _swapper.Reset();
                ImGui.CloseCurrentPopup();
            }
        });

        Modal("已保存", _saver.Value, () =>
        {
            ImGui.TextWrapped($"已成功保存 {_saver.Label}");
            ImGui.Separator();

            var size = new Vector2(120, 0);
            if (ImGui.Button("确定", size))
            {
                _saver.Reset();
                ImGui.CloseCurrentPopup();
            }

            ImGui.SetItemDefaultFocus();
            ImGui.SameLine();

            if (ImGui.Button("在资源管理器中显示", size))
            {
                Process.Start("explorer.exe", $"/select, \"{_saver.Path.Replace('/', '\\')}\"");

                _saver.Reset();
                ImGui.CloseCurrentPopup();
            }
        });
    }

    private void DrawWorld(Snooper s)
    {
        if (ImGui.BeginTable("world_details", 2, ImGuiTableFlags.SizingStretchProp))
        {
            var b = false;
            var length = s.Renderer.Options.Models.Count;

            NoFramePaddingOnY(() =>
            {
                Layout("渲染器");ImGui.Text($" :  {_renderer}");
                Layout("版本");ImGui.Text($" :  {_version}");
                Layout("已加载模型");ImGui.Text($" :  x{length}");ImGui.SameLine();

                if (ImGui.SmallButton("全部保存"))
                {
                    foreach (var model in s.Renderer.Options.Models.Values)
                    {
                        b |= model.Save(out _, out _);
                    }
                }
            });

            Modal("已保存", b, () =>
            {
                ImGui.TextWrapped($"已成功保存 {length} 个模型");
                ImGui.Separator();

                var size = new Vector2(120, 0);
                if (ImGui.Button("确定", size))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SetItemDefaultFocus();
                ImGui.SameLine();

                if (ImGui.Button("在资源管理器中显示", size))
                {
                    Process.Start("explorer.exe", $"/select, \"{UserSettings.Default.ModelDirectory.Replace('/', '\\')}\"");
                    ImGui.CloseCurrentPopup();
                }
            });

            ImGui.EndTable();
        }

        ImGui.SeparatorText("编辑器");
        if (ImGui.BeginTable("world_editor", 2))
        {
            Layout("仅使用旋转播放动画");ImGui.PushID(1);
            ImGui.Checkbox("", ref s.Renderer.AnimateWithRotationOnly);
            ImGui.PopID();Layout("时间倍率");ImGui.PushID(2);
            ImGui.DragFloat("", ref s.Renderer.Options.Tracker.TimeMultiplier, 0.01f, 0.25f, 8f, "x%.2f", ImGuiSliderFlags.NoInput);
            ImGui.PopID();Layout("顶点颜色");ImGui.PushID(3);
            var c = (int) s.Renderer.Color;
            ImGui.Combo("vertex_colors", ref c,
                "默认\0分段\0颜色\0法线\0纹理坐标\0");
            s.Renderer.Color = (VertexColor) c;
            ImGui.PopID();

            ImGui.EndTable();
        }

        ImGui.SeparatorText("相机");
        s.Renderer.CameraOp.ImGuiCamera();

        ImGui.SeparatorText("灯光");
        for (int i = 0; i < s.Renderer.Options.Lights.Count; i++)
        {
            var light = s.Renderer.Options.Lights[i];
            var id = s.Renderer.Options.TryGetModel(light.Model, out var lightModel) ? lightModel.Name : "无";

            id += $"##{i}";
            if (ImGui.TreeNode(id) && ImGui.BeginTable(id, 2))
            {
                s.Renderer.Options.SelectModel(light.Model);
                light.ImGuiLight();
                ImGui.EndTable();
                ImGui.TreePop();
            }
        }
    }

    private void DrawNavbar()
    {
        if (!ImGui.BeginMainMenuBar()) return;

        const int cursorX = 360;
        Modal("命令", ImGui.MenuItem("命令"), () =>
        {
            ImGui.TextWrapped(
                @"大多数操作都比较直观。下面是这个 3D 查看器里常用功能的简要说明：

1. 界面
    - 移动窗口时按住 Shift 可以停靠窗口
    - 双击输入框可以手动输入数值
    - 在输入框中按住鼠标拖动可以直接调整数值
    - 按 H 可以隐藏窗口，并把下一次提取的网格追加进来

2. 视口
    - WASD 移动
    - Shift 加速移动
    - X/C 缩放
    - Z 为选中的模型加载动画
    - 按住鼠标左键观察四周
    - 右键点击场景中的模型可选中它

3. 大纲
    3.1. 右键模型
        - 显示/隐藏模型
        - 切换线框或碰撞显示
        - 保存为 .psk / .pskx
        - 加载动画
        - 将相机快速移动到模型位置
        - 删除、取消选择
        - 复制路径到剪贴板

4. 场景
    - 全部保存会一次保存所有已加载模型
      保存期间界面可能会短暂卡住

5. 详细信息
    5.1. 右键分段
        - 显示/隐藏分段
        - 替换该分段使用的材质
        - 复制路径到剪贴板
    5.2. 变换
        - 移动、旋转、缩放场景中的模型
    5.3. 变形目标
        - 调整顶点形变，改变模型形状

6. 时间轴
    - 空格播放/暂停
    - 可用鼠标控制时间
    - 右键动画条目可让其他模型播放、保存或复制路径
");
            ImGui.Separator();

            ImGui.SetCursorPosX(cursorX);
            ImGui.SetItemDefaultFocus();
            if (ImGui.Button("确定", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
        });

        Modal("GPU OpenGL 信息", ImGui.MenuItem("GPU 信息"), () =>
        {
            var s = new StringBuilder();
            s.AppendLine($"MaxTextureImageUnits: {GL.GetInteger(GetPName.MaxTextureImageUnits)}");
            s.AppendLine($"MaxTextureUnits: {GL.GetInteger(GetPName.MaxTextureUnits)}");
            s.AppendLine($"MaxVertexTextureImageUnits: {GL.GetInteger(GetPName.MaxVertexTextureImageUnits)}");
            s.AppendLine($"MaxCombinedTextureImageUnits: {GL.GetInteger(GetPName.MaxCombinedTextureImageUnits)}");
            s.AppendLine($"MaxGeometryTextureImageUnits: {GL.GetInteger(GetPName.MaxGeometryTextureImageUnits)}");
            s.AppendLine($"MaxTextureCoords: {GL.GetInteger(GetPName.MaxTextureCoords)}");
            s.AppendLine($"Renderer: {_renderer}");
            s.AppendLine($"Version: {_version}");
            ImGui.TextWrapped(s.ToString());
            ImGui.Separator();

            ImGui.SetCursorPosX(cursorX);
            ImGui.SetItemDefaultFocus();
            if (ImGui.Button("确定", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
        });

        Modal("关于 Snooper", ImGui.MenuItem("关于"), () =>
        {
            ImGui.TextWrapped(
                @"Snooper 是一个基于 OpenGL 与 ImGui 的 3D 查看器，用来改进 FModel 的模型、材质、动画和关卡预览能力。

它的目标是在尽量兼容多数 UE 游戏的前提下，提供更准确的模型、材质、骨骼动画、粒子、关卡和关卡动画预览。
");
            ImGui.Separator();

            ImGui.SetCursorPosX(cursorX);
            ImGui.SetItemDefaultFocus();
            if (ImGui.Button("确定", new Vector2(120, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
        });

        const string text = "按 H 隐藏，按 ESC 退出...";
        ImGui.SetCursorPosX(ImGui.GetWindowViewport().WorkSize.X - ImGui.CalcTextSize(text).X - 5);
        ImGui.TextColored(new Vector4(0.36f, 0.42f, 0.47f, 1.00f), text);

        ImGui.EndMainMenuBar();
    }

    private void DrawOuliner(Snooper s)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        Window("大纲", () =>
        {
            _outlinerSize = ImGui.GetWindowSize();
            if (ImGui.BeginTable("Items", 4, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersOuterV | ImGuiTableFlags.NoSavedSettings, ImGui.GetContentRegionAvail()))
            {
                ImGui.TableSetupColumn("实例", ImGuiTableColumnFlags.NoHeaderWidth | ImGuiTableColumnFlags.WidthFixed, _tableWidth);
                ImGui.TableSetupColumn("通道", ImGuiTableColumnFlags.NoHeaderWidth | ImGuiTableColumnFlags.WidthFixed, _tableWidth);
                ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.NoHeaderWidth | ImGuiTableColumnFlags.WidthFixed, _tableWidth);
                ImGui.TableHeadersRow();

                var i = 0;
                foreach ((var guid, var model) in s.Renderer.Options.Models)
                {
                    ImGui.PushID(i);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (!model.IsVisible)
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(1, 0, 0, .5f)));
                    else if (model.Attachments.IsAttachment)
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0, .75f, 0, .5f)));
                    else if (model.Attachments.IsAttached)
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(1, 1, 0, .5f)));

                    ImGui.Text(model.TransformsCount.ToString("D"));
                    ImGui.TableNextColumn();
                    ImGui.Text(model.UvCount.ToString("D"));
                    ImGui.TableNextColumn();
                    var doubleClick = false;
                    if (ImGui.Selectable(model.Name, s.Renderer.Options.SelectedModel == guid, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        s.Renderer.Options.SelectModel(guid);
                        doubleClick = ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
                    }
                    Popup(() =>
                    {
                        s.Renderer.Options.SelectModel(guid);
                        if (ImGui.MenuItem("显示", null, model.IsVisible)) model.IsVisible = !model.IsVisible;
                        if (ImGui.MenuItem("线框", null, model.ShowWireframe)) model.ShowWireframe = !model.ShowWireframe;
                        if (ImGui.MenuItem("碰撞", null, model.ShowCollisions, model.HasCollisions)) model.ShowCollisions = !model.ShowCollisions;
                        ImGui.Separator();
                        if (ImGui.MenuItem("保存"))
                        {
                            s.WindowShouldFreeze(true);
                            _saver.Value = model.Save(out _saver.Label, out _saver.Path);
                            s.WindowShouldFreeze(false);
                        }
                        if (ImGui.MenuItem("动画", model is SkeletalModel))
                        {
                            if (_swapper.IsAware)
                            {
                                s.Renderer.Options.RemoveAnimations();
                                s.Renderer.Options.AnimateMesh(true);
                                s.WindowShouldClose(true, false);
                            }
                            else
                            {
                                _swapper.Title = "骨骼动画";
                                _swapper.Description = "即将为模型加载动画。\n窗口会关闭，以便你提取一个动画资源。\n\n";
                                _swapper.Content = () =>
                                {
                                    s.Renderer.Options.RemoveAnimations();
                                    s.Renderer.Options.AnimateMesh(true);
                                };
                                _swapper.Value = true;
                            }
                        }
                        if (ImGui.MenuItem("骨骼树", model is SkeletalModel))
                        {
                            s.Renderer.IsSkeletonTreeOpen = true;
                            ImGui.SetWindowFocus("骨骼树");
                        }
                        doubleClick = ImGui.MenuItem("移动相机到此处");

                        if (ImGui.MenuItem("删除")) s.Renderer.Options.RemoveModel(guid);
                        if (ImGui.MenuItem("取消选择")) s.Renderer.Options.SelectModel(Guid.Empty);
                        ImGui.Separator();
                        if (ImGui.MenuItem("复制路径到剪贴板")) ImGui.SetClipboardText(model.Path);
                    });
                    if (doubleClick)
                    {
                        s.Renderer.CameraOp.Teleport(model.GetTransform().Matrix.Translation, model.Box);
                    }

                    ImGui.TableNextColumn();
                    ImGui.Image(s.Renderer.Options.Icons[model.Attachments.Icon].GetPointer(), new Vector2(_tableWidth));
                    TooltipCopy(model.Attachments.Tooltip);

                    ImGui.PopID();
                    i++;
                }

                ImGui.EndTable();
            }
        });
        ImGui.PopStyleVar();
    }

    private void DrawSockets(Snooper s)
    {
        MeshWindow("插槽", s.Renderer, (icons, selectedModel) =>
        {
            var info = new SocketAttachementInfo { Guid = s.Renderer.Options.SelectedModel, Instance = selectedModel.SelectedInstance };
            foreach (var model in s.Renderer.Options.Models.Values)
            {
                if (!model.HasSockets || model.IsSelected) continue;
                if (ImGui.TreeNode($"{model.Name} [{model.Sockets.Count}]"))
                {
                    var i = 0;
                    foreach (var socket in model.Sockets)
                    {
                        var isAttached = socket.AttachedModels.Contains(info);
                        ImGui.PushID(i);
                        ImGui.BeginDisabled(selectedModel.Attachments.IsAttached && !isAttached);
                        switch (isAttached)
                        {
                            case false when ImGui.Button($"附加到 '{socket.Name}'"):
                                selectedModel.Attachments.Attach(model, selectedModel.GetTransform(), socket, info);
                                break;
                            case true when ImGui.Button($"从 '{socket.Name}' 分离"):
                                selectedModel.Attachments.Detach(model, selectedModel.GetTransform(), socket, info);
                                break;
                        }
                        ImGui.EndDisabled();
                        ImGui.PopID();
                        i++;
                    }
                    ImGui.TreePop();
                }
            }
        });
    }

    private void DrawDetails(Snooper s)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        MeshWindow("详细信息", s.Renderer, (icons, model) =>
        {
            if (ImGui.BeginTable("model_details", 2, ImGuiTableFlags.SizingStretchProp))
            {
                NoFramePaddingOnY(() =>
                {
                    Layout("实体");ImGui.Text($"  :  ({model.Type}) {model.Name}");
                    Layout("Guid");ImGui.Text($"  :  {s.Renderer.Options.SelectedModel.ToString(EGuidFormats.UniqueObjectGuid)}");
                    if (model is SkeletalModel skeletalModel)
                    {
                        Layout("骨架");ImGui.Text($"  :  {skeletalModel.Skeleton.Name}");
                        Layout("骨骼");ImGui.Text($"  :  x{skeletalModel.Skeleton.BoneCount}");
                    }
                    else
                    {
                        Layout("双面");ImGui.Text($"  :  {model.IsTwoSided}");
                    }
                    Layout("插槽");ImGui.Text($"  :  x{model.Sockets.Count}");

                    ImGui.EndTable();
                });
            }
            if (ImGui.BeginTabBar("tabbar_details", ImGuiTabBarFlags.None))
            {
                if (ImGui.BeginTabItem("分段") && ImGui.BeginTable("table_sections", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersOuterV | ImGuiTableFlags.NoSavedSettings, ImGui.GetContentRegionAvail()))
                {
                    ImGui.TableSetupColumn("索引", ImGuiTableColumnFlags.NoHeaderWidth | ImGuiTableColumnFlags.WidthFixed, _tableWidth);
                    ImGui.TableSetupColumn("材质");
                    ImGui.TableHeadersRow();

                    for (var i = 0; i < model.Sections.Length; i++)
                    {
                        var section = model.Sections[i];
                        var material = model.Materials[section.MaterialIndex];

                        ImGui.PushID(i);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        if (!section.Show)
                        {
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(1, 0, 0, .5f)));
                        }
                        else if (s.Renderer.Color == VertexColor.Sections)
                        {
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(section.Color, 0.5f)));
                        }

                        ImGui.Text(section.MaterialIndex.ToString("D"));
                        ImGui.TableNextColumn();
                        if (ImGui.Selectable(material.Name, s.Renderer.Options.SelectedSection == i, ImGuiSelectableFlags.SpanAllColumns))
                        {
                            s.Renderer.Options.SelectSection(i);
                        }
                        Popup(() =>
                        {
                            s.Renderer.Options.SelectSection(i);
                            if (ImGui.MenuItem("显示", null, section.Show)) section.Show = !section.Show;
                            if (ImGui.MenuItem("替换"))
                            {
                                if (_swapper.IsAware)
                                {
                                    s.Renderer.Options.SwapMaterial(true);
                                    s.WindowShouldClose(true, false);
                                }
                                else
                                {
                                    _swapper.Title = "替换材质";
                                    _swapper.Description = "即将替换材质。\n窗口会关闭，以便你提取一个材质资源。\n\n";
                                    _swapper.Content = () => s.Renderer.Options.SwapMaterial(true);
                                    _swapper.Value = true;
                                }
                            }
                            ImGui.Separator();
                            if (ImGui.MenuItem("复制路径到剪贴板")) ImGui.SetClipboardText(material.Path);
                        });
                        ImGui.PopID();
                    }
                    ImGui.EndTable();

                    ImGui.EndTabItem();
                }

                _transformOpen = ImGui.BeginTabItem("变换");
                if (_transformOpen)
                {
                    ImGui.PushID(0); ImGui.BeginDisabled(model.TransformsCount < 2);
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    ImGui.SliderInt("", ref model.SelectedInstance, 0, model.TransformsCount - 1, "实例 %i", ImGuiSliderFlags.AlwaysClamp);
                    ImGui.EndDisabled(); ImGui.PopID();

                    if (ImGui.BeginTable("guizmo_controls", 2, ImGuiTableFlags.SizingStretchProp))
                    {
                        var t = model.Transforms[model.SelectedInstance];
                        var c = _guizmoOperation switch
                        {
                            OPERATION.TRANSLATE => 0,
                            OPERATION.ROTATE => 1,
                            OPERATION.SCALE => 2,
                            _ => 3
                        };

                        Layout("操作          ");
                        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.6f);
                        ImGui.PushID(1);ImGui.Combo("", ref c, "移动\0旋转\0缩放\0");
                        ImGui.PopID();ImGui.SameLine();if (ImGui.Button("全部重置")) t.Reset();
                        Layout("位置");ImGui.Text(t.Position.ToString());
                        Layout("旋转");ImGui.Text(t.Rotation.ToString());
                        Layout("缩放");ImGui.Text(t.Scale.ToString());

                        _guizmoOperation = c switch
                        {
                            0 => OPERATION.TRANSLATE,
                            1 => OPERATION.ROTATE,
                            2 => OPERATION.SCALE,
                            _ => OPERATION.UNIVERSAL
                        };

                        ImGui.EndTable();
                    }

                    ImGui.SeparatorText("手动输入");
                    model.Transforms[model.SelectedInstance].ImGuiTransform(s.Renderer.CameraOp.Speed / 100f);

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("变形目标"))
                {
                    if (model is SkeletalModel { HasMorphTargets: true } skeletalModel)
                    {
                        const float width = 10;
                        var region = ImGui.GetContentRegionAvail();
                        var box = new Vector2(region.X - width, region.Y / 1.5f);

                        if (ImGui.BeginListBox("", box))
                        {
                            for (int i = 0; i < skeletalModel.Morphs.Count; i++)
                            {
                                ImGui.PushID(i);
                                if (ImGui.Selectable(skeletalModel.Morphs[i].Name, s.Renderer.Options.SelectedMorph == i))
                                {
                                    s.Renderer.Options.SelectMorph(i, skeletalModel);
                                }
                                ImGui.PopID();
                            }
                            ImGui.EndListBox();

                            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2f, 0f));
                            ImGui.SameLine(); ImGui.PushID(99);
                            ImGui.VSliderFloat("", box with { X = width }, ref skeletalModel.MorphTime, 0.0f, 1.0f, "", ImGuiSliderFlags.AlwaysClamp);
                            ImGui.PopID(); ImGui.PopStyleVar();
                            ImGui.Spacing();
                            ImGui.Text($"时间: {skeletalModel.MorphTime:P}%");
                        }
                    }
                    else CenteredTextColored(_errorColor, "选中的网格没有变形目标");
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        });
        ImGui.PopStyleVar();
    }

    private void DrawMaterialInspector(Dictionary<string, Texture> icons, UModel model, Section section)
    {
        var material = model.Materials[section.MaterialIndex];

        ImGui.Spacing();
        ImGui.Image(icons["material"].GetPointer(), new Vector2(24));
        ImGui.SameLine(); ImGui.AlignTextToFramePadding(); ImGui.Text(material.Name);
        ImGui.Spacing();

        ImGui.SeparatorText("参数");
        material.ImGuiParameters();

        ImGui.SeparatorText("纹理");
        if (material.ImGuiTextures(icons, model))
        {
            _tiOpen = true;
            ImGui.SetWindowFocus("纹理检查器");
        }

        ImGui.SeparatorText("属性");
        NoFramePaddingOnY(() =>
        {
            ImGui.SetNextItemOpen(true, ImGuiCond.Appearing);
            if (ImGui.TreeNode("基础"))
            {
                material.ImGuiBaseProperties("base");
                ImGui.TreePop();
            }

            ImGui.SetNextItemOpen(true, ImGuiCond.Appearing);
            if (ImGui.TreeNode("标量"))
            {
                material.ImGuiDictionaries("scalars", material.Parameters.Scalars, true);
                ImGui.TreePop();
            }
            ImGui.SetNextItemOpen(true, ImGuiCond.Appearing);
            if (ImGui.TreeNode("开关"))
            {
                material.ImGuiDictionaries("switches", material.Parameters.Switches, true);
                ImGui.TreePop();
            }
            ImGui.SetNextItemOpen(true, ImGuiCond.Appearing);
            if (ImGui.TreeNode("颜色"))
            {
                material.ImGuiColors(material.Parameters.Colors);
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("全部纹理"))
            {
                material.ImGuiDictionaries("textures", material.Parameters.Textures);
                ImGui.TreePop();
            }
        });
    }

    private void DrawTextureInspector(Snooper s)
    {
        if (!_tiOpen) return;
        if (ImGui.Begin("纹理检查器", ref _tiOpen, ImGuiWindowFlags.NoScrollbar))
        {
            if (s.Renderer.Options.TryGetModel(out var model) && s.Renderer.Options.TryGetSection(model, out var section))
            {
                (model.Materials[section.MaterialIndex].GetSelectedTexture() ?? s.Renderer.Options.Icons["noimage"]).ImGuiTextureInspector();
            }
        }
        ImGui.End();
    }

    private void DrawSkeletonTree(Snooper s)
    {
        if (!s.Renderer.IsSkeletonTreeOpen) return;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        if (ImGui.Begin("骨骼树", ref s.Renderer.IsSkeletonTreeOpen, ImGuiWindowFlags.NoScrollbar))
        {
            if (s.Renderer.Options.TryGetModel(out var model) && model is SkeletalModel skeletalModel)
            {
                skeletalModel.Skeleton.ImGuiBoneBreadcrumb();
                if (ImGui.BeginTable("skeleton_tree", 2, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.RowBg, ImGui.GetContentRegionAvail(), ImGui.GetWindowWidth()))
                {
                    ImGui.TableSetupColumn("骨骼", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.NoHeaderWidth | ImGuiTableColumnFlags.WidthFixed, _tableWidth);
                    skeletalModel.Skeleton.ImGuiBoneHierarchy();
                    ImGui.EndTable();
                }
            }
        }
        ImGui.End();
        ImGui.PopStyleVar();
    }

    private void Draw3DViewport(Snooper s)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        Window("3D Viewport", () =>
        {
            var largest = ImGui.GetContentRegionAvail();
            largest.X -= ImGui.GetScrollX();
            largest.Y -= ImGui.GetScrollY();

            var size = new Vector2(largest.X, largest.Y);
            var pos = ImGui.GetWindowPos();
            var fHeight = ImGui.GetFrameHeight();

            s.Renderer.CameraOp.AspectRatio = size.X / size.Y;
            ImGui.Image(s.Framebuffer.GetPointer(), size, new Vector2(0, 1), new Vector2(1, 0));

            if (_transformOpen)
            {
                ImGuizmo.SetDrawlist(ImGui.GetWindowDrawList());
                ImGuizmo.SetRect(pos.X, pos.Y + fHeight, size.X, size.Y);
                DrawGuizmo(s);
            }

            if (!ImGuizmo.IsUsing())
            {
                if (ImGui.IsItemHovered())
                {
                    // if left button down while mouse is hover viewport
                    if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && !_viewportFocus)
                    {
                        _viewportFocus = true;
                        s.CursorState = CursorState.Grabbed;
                    }
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        var guid = s.Renderer.Picking.ReadPixel(ImGui.GetMousePos(), ImGui.GetCursorScreenPos(), size);
                        s.Renderer.Options.SelectModel(guid);
                        ImGui.SetWindowFocus("大纲");
                        ImGui.SetWindowFocus("详细信息");
                    }
                }

                if (_viewportFocus && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    s.Renderer.CameraOp.Modify(ImGui.GetIO().MouseDelta);
                }

                // if left button up and mouse was in viewport
                if (_viewportFocus && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    _viewportFocus = false;
                    s.CursorState = CursorState.Normal;
                }
            }

            const float margin = 7.5f;
            var buttonWidth = 14.0f * ImGui.GetWindowDpiScale();
            var basePos = new Vector2( size.X - buttonWidth - margin * 2, fHeight + margin);
            ImGui.SetCursorPos(basePos);
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f));
            ImGui.ImageButton("skybox_btn", s.Renderer.Options.Icons[s.Renderer.ShowSkybox ? "cube" : "cube_off"].GetPointer(), new Vector2(buttonWidth));
            TooltipCheckbox("天空盒", ref s.Renderer.ShowSkybox);

            basePos.X -= buttonWidth + margin;
            ImGui.SetCursorPos(basePos);
            ImGui.ImageButton("grid_btn", s.Renderer.Options.Icons[s.Renderer.ShowGrid ? "square" : "square_off"].GetPointer(), new Vector2(buttonWidth));
            TooltipCheckbox("网格", ref s.Renderer.ShowGrid);

            basePos.X -= buttonWidth + margin;
            ImGui.SetCursorPos(basePos);
            ImGui.ImageButton("lights_btn", s.Renderer.Options.Icons[s.Renderer.ShowLights ? "light" : "light_off"].GetPointer(), new Vector2(buttonWidth));
            TooltipCheckbox("灯光", ref s.Renderer.ShowLights);

            ImGui.PopStyleColor(2);

            float framerate = ImGui.GetIO().Framerate;
            ImGui.SetCursorPos(size with { X = margin });
            ImGui.Text($"FPS: {framerate:0} ({1000.0f / framerate:0.##} ms)");

            const string label = "预览内容可能与保存结果或游戏内最终效果不同。";
            ImGui.SetCursorPos(size with { X = size.X - ImGui.CalcTextSize(label).X - margin });
            ImGui.TextColored(new Vector4(0.50f, 0.50f, 0.50f, 1.00f), label);

        }, false);
        ImGui.PopStyleVar();
    }

    private void DrawGuizmo(Snooper s)
    {
        var enableGuizmo = s.Renderer.Options.TryGetModel(out var selected) && selected.IsVisible;
        if (enableGuizmo)
        {
            var view = s.Renderer.CameraOp.GetViewMatrix();
            var proj = s.Renderer.CameraOp.GetProjectionMatrix();
            var transform = selected.Transforms[selected.SelectedInstance];
            var matrix = transform.Matrix;

            if (ImGuizmo.Manipulate(ref view.M11, ref proj.M11, _guizmoOperation, MODE.LOCAL, ref matrix.M11) &&
                Matrix4x4.Invert(transform.Relation, out var invRelation))
            {
                // ^ long story short: there was issues with other transformation methods
                // that's one way of modifying root elements without breaking the world matrix
                transform.ModifyLocal(matrix * invRelation);
            }
        }
    }

    public static void Popup(Action content)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4f));
        if (ImGui.BeginPopupContextItem())
        {
            content();
            ImGui.EndPopup();
        }
        ImGui.PopStyleVar();
    }

    private void Modal(string title, bool condition, Action content)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4f));
        var pOpen = true;
        if (condition) ImGui.OpenPopup(title);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(.5f));
        if (ImGui.BeginPopupModal(title, ref pOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            content();
            ImGui.EndPopup();
        }
        ImGui.PopStyleVar();
    }

    private void Window(string name, Action content, bool styled = true)
    {
        if (ImGui.Begin(name, ImGuiWindowFlags.NoScrollbar))
        {
            Controller.Normal();
            if (styled) PushStyleCompact();
            content();
            if (styled) PopStyleCompact();
            ImGui.PopFont();
        }
        ImGui.End();
    }

    private void MeshWindow(string name, Renderer renderer, Action<Dictionary<string, Texture>, UModel> content, bool styled = true)
    {
        Window(name, () =>
        {
            if (renderer.Options.TryGetModel(out var model)) content(renderer.Options.Icons, model);
            else NoMeshSelected();
        }, styled);
    }

    private void SectionWindow(string name, Renderer renderer, Action<Dictionary<string, Texture>, UModel, Section> content, bool styled = true)
    {
        MeshWindow(name, renderer, (icons, model) =>
        {
            if (renderer.Options.TryGetSection(model, out var section)) content(icons, model, section);
            else NoSectionSelected();
        }, styled);
    }

    private void AnimationWindow(string name, Renderer renderer, Action<Dictionary<string, Texture>, TimeTracker, List<Animation>> content, bool styled = true)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        Window(name, () => content(renderer.Options.Icons, renderer.Options.Tracker, renderer.Options.Animations), styled);
        ImGui.PopStyleVar();
    }

    private void PopStyleCompact() => ImGui.PopStyleVar(2);
    private void PushStyleCompact()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 3));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(0, 1));
    }

    public static void NoFramePaddingOnY(Action content)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 0));
        content();
        ImGui.PopStyleVar();
    }

    private void NoMeshSelected() => CenteredTextColored(_errorColor, "未选择网格");
    private void NoSectionSelected() => CenteredTextColored(_errorColor, "未选择分段");
    private void CenteredTextColored(Vector4 color, string text)
    {
        var region = ImGui.GetContentRegionAvail();
        var size = ImGui.CalcTextSize(text);
        ImGui.SetCursorPos(new Vector2(
                ImGui.GetCursorPosX() + (region.X - size.X) / 2,
                ImGui.GetCursorPosY() + (region.Y - size.Y) / 2));
        Controller.Bold();
        ImGui.TextColored(color, text);
        ImGui.PopFont();
    }

    public static void Layout(string name, bool tooltip = false)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.Spacing();ImGui.SameLine();ImGui.Text(name);
        if (tooltip) TooltipCopy(name);
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
    }

    public static void TooltipCopy(string label, string text = null)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(label);
            ImGui.EndTooltip();
        }
        if (ImGui.IsItemClicked()) ImGui.SetClipboardText(text ?? label);
    }

    private static void TooltipCheckbox(string tooltip, ref bool value)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text($"{tooltip}: {value}");
            ImGui.EndTooltip();
        }
        if (ImGui.IsItemClicked()) value = !value;
    }

    private void Theme()
    {
        var style = ImGui.GetStyle();
        style.WindowPadding = new Vector2(4f);
        style.FramePadding = new Vector2(3f);
        style.CellPadding = new Vector2(3f, 2f);
        style.ItemSpacing = new Vector2(6f, 3f);
        style.ItemInnerSpacing = new Vector2(3f);
        style.TouchExtraPadding = new Vector2(0f);
        style.IndentSpacing = 20f;
        style.ScrollbarSize = 10f;
        style.GrabMinSize = 8f;
        style.WindowBorderSize = 0f;
        style.ChildBorderSize = 0f;
        style.PopupBorderSize = 0f;
        style.FrameBorderSize = 0f;
        style.TabBorderSize = 0f;
        style.WindowRounding = 0f;
        style.ChildRounding = 0f;
        style.FrameRounding = 0f;
        style.PopupRounding = 0f;
        style.ScrollbarRounding = 0f;
        style.GrabRounding = 0f;
        style.LogSliderDeadzone = 0f;
        style.TabRounding = 0f;
        style.WindowTitleAlign = new Vector2(0.5f);
        style.WindowMenuButtonPosition = ImGuiDir.Right;
        style.ColorButtonPosition = ImGuiDir.Right;
        style.ButtonTextAlign = new Vector2(0.5f);
        style.SelectableTextAlign = new Vector2(0f);
        style.DisplaySafeAreaPadding = new Vector2(3f);

        style.Colors[(int) ImGuiCol.Text]                   = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
        style.Colors[(int) ImGuiCol.TextDisabled]           = new Vector4(0.50f, 0.50f, 0.50f, 1.00f);
        style.Colors[(int) ImGuiCol.WindowBg]               = new Vector4(0.11f, 0.11f, 0.12f, 1.00f);
        style.Colors[(int) ImGuiCol.ChildBg]                = new Vector4(0.15f, 0.15f, 0.19f, 1.00f);
        style.Colors[(int) ImGuiCol.PopupBg]                = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);
        style.Colors[(int) ImGuiCol.Border]                 = new Vector4(0.25f, 0.26f, 0.33f, 1.00f);
        style.Colors[(int) ImGuiCol.BorderShadow]           = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        style.Colors[(int) ImGuiCol.FrameBg]                = new Vector4(0.05f, 0.05f, 0.05f, 0.54f);
        style.Colors[(int) ImGuiCol.FrameBgHovered]         = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        style.Colors[(int) ImGuiCol.FrameBgActive]          = new Vector4(0.69f, 0.69f, 1.00f, 0.39f);
        style.Colors[(int) ImGuiCol.TitleBg]                = new Vector4(0.09f, 0.09f, 0.09f, 1.00f);
        style.Colors[(int) ImGuiCol.TitleBgActive]          = new Vector4(0.09f, 0.09f, 0.09f, 1.00f);
        style.Colors[(int) ImGuiCol.TitleBgCollapsed]       = new Vector4(0.05f, 0.05f, 0.05f, 0.51f);
        style.Colors[(int) ImGuiCol.MenuBarBg]              = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
        style.Colors[(int) ImGuiCol.ScrollbarBg]            = new Vector4(0.02f, 0.02f, 0.02f, 0.53f);
        style.Colors[(int) ImGuiCol.ScrollbarGrab]          = new Vector4(0.31f, 0.31f, 0.31f, 1.00f);
        style.Colors[(int) ImGuiCol.ScrollbarGrabHovered]   = new Vector4(0.41f, 0.41f, 0.41f, 1.00f);
        style.Colors[(int) ImGuiCol.ScrollbarGrabActive]    = new Vector4(0.51f, 0.51f, 0.51f, 1.00f);
        style.Colors[(int) ImGuiCol.CheckMark]              = new Vector4(0.13f, 0.42f, 0.83f, 1.00f);
        style.Colors[(int) ImGuiCol.SliderGrab]             = new Vector4(0.13f, 0.42f, 0.83f, 0.78f);
        style.Colors[(int) ImGuiCol.SliderGrabActive]       = new Vector4(0.13f, 0.42f, 0.83f, 1.00f);
        style.Colors[(int) ImGuiCol.Button]                 = new Vector4(0.05f, 0.05f, 0.05f, 0.54f);
        style.Colors[(int) ImGuiCol.ButtonHovered]          = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        style.Colors[(int) ImGuiCol.ButtonActive]           = new Vector4(0.69f, 0.69f, 1.00f, 0.39f);
        style.Colors[(int) ImGuiCol.Header]                 = new Vector4(0.05f, 0.26f, 0.56f, 1.00f);
        style.Colors[(int) ImGuiCol.HeaderHovered]          = new Vector4(0.05f, 0.26f, 0.56f, 0.39f);
        style.Colors[(int) ImGuiCol.HeaderActive]           = new Vector4(0.04f, 0.23f, 0.52f, 1.00f);
        style.Colors[(int) ImGuiCol.Separator]              = new Vector4(0.43f, 0.43f, 0.50f, 0.50f);
        style.Colors[(int) ImGuiCol.SeparatorHovered]       = new Vector4(0.10f, 0.40f, 0.75f, 0.78f);
        style.Colors[(int) ImGuiCol.SeparatorActive]        = new Vector4(0.10f, 0.40f, 0.75f, 1.00f);
        style.Colors[(int) ImGuiCol.ResizeGrip]             = new Vector4(0.13f, 0.42f, 0.83f, 0.39f);
        style.Colors[(int) ImGuiCol.ResizeGripHovered]      = new Vector4(0.12f, 0.41f, 0.81f, 0.78f);
        style.Colors[(int) ImGuiCol.ResizeGripActive]       = new Vector4(0.12f, 0.41f, 0.81f, 1.00f);
        style.Colors[(int) ImGuiCol.Tab]                    = new Vector4(0.15f, 0.15f, 0.19f, 1.00f);
        style.Colors[(int) ImGuiCol.TabHovered]             = new Vector4(0.35f, 0.35f, 0.41f, 0.80f);
        style.Colors[(int) ImGuiCol.TabSelected]            = new Vector4(0.23f, 0.24f, 0.29f, 1.00f);
        style.Colors[(int) ImGuiCol.TabDimmed]              = new Vector4(0.15f, 0.15f, 0.15f, 1.00f);
        style.Colors[(int) ImGuiCol.TabDimmedSelected]      = new Vector4(0.23f, 0.24f, 0.29f, 1.00f);
        style.Colors[(int) ImGuiCol.DockingPreview]         = new Vector4(0.26f, 0.59f, 0.98f, 0.70f);
        style.Colors[(int) ImGuiCol.DockingEmptyBg]         = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
        style.Colors[(int) ImGuiCol.PlotLines]              = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);
        style.Colors[(int) ImGuiCol.PlotLinesHovered]       = new Vector4(1.00f, 0.43f, 0.35f, 1.00f);
        style.Colors[(int) ImGuiCol.PlotHistogram]          = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
        style.Colors[(int) ImGuiCol.PlotHistogramHovered]   = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
        style.Colors[(int) ImGuiCol.TableHeaderBg]          = new Vector4(0.09f, 0.09f, 0.09f, 1.00f);
        style.Colors[(int) ImGuiCol.TableBorderStrong]      = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        style.Colors[(int) ImGuiCol.TableBorderLight]       = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        style.Colors[(int) ImGuiCol.TableRowBg]             = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        style.Colors[(int) ImGuiCol.TableRowBgAlt]          = new Vector4(1.00f, 1.00f, 1.00f, 0.06f);
        style.Colors[(int) ImGuiCol.TextSelectedBg]         = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);
        style.Colors[(int) ImGuiCol.DragDropTarget]         = new Vector4(1.00f, 1.00f, 0.00f, 0.90f);
        style.Colors[(int) ImGuiCol.NavCursor]              = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
        style.Colors[(int) ImGuiCol.NavWindowingHighlight]  = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);
        style.Colors[(int) ImGuiCol.NavWindowingDimBg]      = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);
        style.Colors[(int) ImGuiCol.ModalWindowDimBg]       = new Vector4(0.80f, 0.80f, 0.80f, 0.35f);
    }
}
