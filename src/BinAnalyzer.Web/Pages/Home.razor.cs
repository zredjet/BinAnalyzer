using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using BinAnalyzer.Core;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using BinAnalyzer.Web.Services;

namespace BinAnalyzer.Web.Pages;

public partial class Home
{
    private List<FormatEntry> _formats = [];
    private string _selectedFormatFile = "";
    private string? _fileName;
    private byte[]? _fileData;
    private string? _htmlResult;
    private string? _jsonResult;
    private string? _errorMessage;
    private bool _isDecoding;
    private bool _isDragOver;
    private ElementReference _iframeRef;

    private bool CanDecode => _fileData is not null
        && !string.IsNullOrEmpty(_selectedFormatFile)
        && !_isDecoding;

    protected override async Task OnInitializedAsync()
    {
        _formats = await FormatService.GetFormatListAsync();
    }

    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        _errorMessage = null;
        _htmlResult = null;
        _jsonResult = null;

        var file = e.File;
        if (file.Size > 100 * 1024 * 1024)
        {
            _errorMessage = "ファイルサイズが大きすぎます（上限: 100MB）。";
            return;
        }

        _fileName = file.Name;
        using var stream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        _fileData = ms.ToArray();

        var ext = Path.GetExtension(file.Name);
        if (!string.IsNullOrEmpty(ext))
        {
            var detected = await FormatService.DetectFormat(ext);
            if (detected is not null)
            {
                _selectedFormatFile = detected.File;
            }
        }
    }

    private async Task Decode()
    {
        if (_fileData is null || string.IsNullOrEmpty(_selectedFormatFile))
            return;

        _isDecoding = true;
        _errorMessage = null;
        _htmlResult = null;
        _jsonResult = null;
        StateHasChanged();

        try
        {
            await Task.Yield();

            var format = await FormatService.LoadFormatAsync(_selectedFormatFile);

            var validation = FormatValidator.Validate(format);
            if (!validation.IsValid)
            {
                _errorMessage = "フォーマット定義エラー: " +
                    string.Join("; ", validation.Errors.Select(e => e.Message));
                return;
            }

            var decoder = new BinaryDecoder();
            var decoded = decoder.Decode(_fileData.AsMemory(), format);

            var htmlFormatter = new HtmlOutputFormatter();
            _htmlResult = htmlFormatter.Format(decoded);

            var jsonFormatter = new JsonOutputFormatter();
            _jsonResult = jsonFormatter.Format(decoded);
        }
        catch (DecodeException ex)
        {
            _errorMessage = $"デコードエラー: {ex.Message}";
        }
        catch (Exception ex)
        {
            _errorMessage = $"エラー: {ex.Message}";
        }
        finally
        {
            _isDecoding = false;
        }
    }

    private async Task DownloadJson()
    {
        if (_jsonResult is null || _fileName is null)
            return;

        var jsonFileName = Path.GetFileNameWithoutExtension(_fileName) + ".json";
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(_jsonResult));
        await JS.InvokeVoidAsync("downloadFile", jsonFileName, "application/json", base64);
    }

    private void HandleDragOver()
    {
        _isDragOver = true;
    }

    private void HandleDragLeave()
    {
        _isDragOver = false;
    }

    internal static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB",
        };
    }
}
