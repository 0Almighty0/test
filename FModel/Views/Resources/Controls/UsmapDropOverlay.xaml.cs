using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FModel.Services;
using FModel.Settings;
using FModel.ViewModels;

namespace FModel.Views.Resources.Controls;

public partial class UsmapDropOverlay : UserControl
{
    private ApplicationViewModel _applicationView => ApplicationService.ApplicationView;
    private bool _isDraggingUsmap = false;

    public UsmapDropOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null)
            return;

        window.PreviewDragEnter += OnPreviewDragEnter;
        window.PreviewDragOver += OnPreviewDragOver;
        window.PreviewDragLeave += OnPreviewDragLeave;
        window.Drop += OnDrop;
    }

    private void OnPreviewDragEnter(object sender, DragEventArgs e)
    {
        _isDraggingUsmap = CheckIsUsmap(e);
        if (_isDraggingUsmap)
        {
            Visibility = Visibility.Visible;
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!_isDraggingUsmap)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }
    }

    private void OnPreviewDragLeave(object sender, DragEventArgs e) =>
        Visibility = Visibility.Collapsed;

    private async void OnDrop(object sender, DragEventArgs e)
    {
        Visibility = Visibility.Collapsed;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[]) e.Data.GetData(DataFormats.FileDrop);
        var usmapFile = files.FirstOrDefault(file => Path.GetExtension(file).Equals(".usmap", StringComparison.OrdinalIgnoreCase));

        if (usmapFile is null)
            return;

        UserSettings.IsEndpointValid(EEndpointType.Mapping, out var oldMappingsEndpoint);
        try
        {
            var newMappingsEndpoint = new EndpointSettings() { Overwrite = true, FilePath = usmapFile };
            UserSettings.Default.CurrentDir.Endpoints[(int) EEndpointType.Mapping] = newMappingsEndpoint;
            await _applicationView.CUE4Parse.InitMappings();
            _applicationView.SettingsView.MappingEndpoint = newMappingsEndpoint;
        }
        catch (Exception ex)
        {
            UserSettings.Default.CurrentDir.Endpoints[(int) EEndpointType.Mapping] = oldMappingsEndpoint;
            FLogger.Append(ELog.Error, () =>
            {
                FLogger.Text($"Failed to load mapping file: {ex.Message}", Constants.WHITE, true);
            });
        }
    }

    private bool CheckIsUsmap(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        var files = (string[]) e.Data.GetData(DataFormats.FileDrop);
        return _applicationView.Status.IsReady && files.Any(f => Path.GetExtension(f).Equals(".usmap", StringComparison.OrdinalIgnoreCase));
    }
}
