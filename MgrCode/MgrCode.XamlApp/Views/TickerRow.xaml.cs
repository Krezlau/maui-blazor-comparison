using System.ComponentModel;
using Microsoft.Maui.Graphics;
using MgrCode.Backend.Models;
using MgrCode.Backend.Services;

namespace MgrCode.XamlApp.Views;

public partial class TickerRow : ContentView
{
    private static readonly Color Gainer = Color.FromArgb("#16A34A");
    private static readonly Color Loser = Color.FromArgb("#DC2626");
    private static readonly Color DefaultText = Color.FromArgb("#1F2937");
    private static readonly Color FlashUpBg = Color.FromArgb("#B7F7C8");
    private static readonly Color FlashDownBg = Color.FromArgb("#FFC1C1");
    private static readonly Color PctUpBg = Color.FromArgb("#E8F8EE");
    private static readonly Color PctDownBg = Color.FromArgb("#FDEBEB");

    private readonly SparklineDrawable _drawable = new();
    private Ticker? _ticker;
    private CancellationTokenSource? _flashCts;

    public TickerRow()
    {
        InitializeComponent();
        Sparkline.Drawable = _drawable;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        Unsubscribe();
        ResetFlash();

        _ticker = BindingContext as Ticker;
        _drawable.Ticker = _ticker;

        if (_ticker is null)
            return;

        RenderAll(_ticker);
        _ticker.PropertyChanged += OnTickerPropertyChanged;
    }

    private void OnTickerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var ticker = _ticker;
        if (ticker is null)
            return;

        switch (e.PropertyName)
        {
            case nameof(Ticker.LastPrice):
                UpdateLast(ticker);
                Flash(ticker.Direction);
                break;
            case nameof(Ticker.Price24hPcnt):
                UpdatePct(ticker);
                break;
            case nameof(Ticker.Volume24h):
                VolumeLabel.Text = Formatting.FormatCompact(ticker.Volume24h);
                break;
            case nameof(Ticker.HighPrice24h):
            case nameof(Ticker.LowPrice24h):
                HighLowLabel.Text = Formatting.FormatPrice(ticker.HighPrice24h, ticker.Symbol)
                    + " / " + Formatting.FormatPrice(ticker.LowPrice24h, ticker.Symbol);
                break;
            case nameof(Ticker.Bid1Price):
            case nameof(Ticker.Ask1Price):
                BidAskLabel.Text = Formatting.FormatPrice(ticker.Bid1Price, ticker.Symbol)
                    + " / " + Formatting.FormatPrice(ticker.Ask1Price, ticker.Symbol);
                break;
            case nameof(Ticker.SparklineVersion):
                Sparkline.Invalidate();
                break;
        }
    }

    private void RenderAll(Ticker ticker)
    {
        SymbolLabel.Text = ticker.Symbol;
        Dot.Fill = new SolidColorBrush(Color.FromArgb(Formatting.ColorFromSymbol(ticker.Symbol)));

        UpdateLast(ticker);
        UpdatePct(ticker);
        VolumeLabel.Text = Formatting.FormatCompact(ticker.Volume24h);
        HighLowLabel.Text = Formatting.FormatPrice(ticker.HighPrice24h, ticker.Symbol)
            + " / " + Formatting.FormatPrice(ticker.LowPrice24h, ticker.Symbol);
        BidAskLabel.Text = Formatting.FormatPrice(ticker.Bid1Price, ticker.Symbol)
            + " / " + Formatting.FormatPrice(ticker.Ask1Price, ticker.Symbol);

        Sparkline.Invalidate();
    }

    private void UpdateLast(Ticker ticker)
    {
        LastLabel.Text = Formatting.FormatPrice(ticker.LastPrice, ticker.Symbol);
        LastLabel.TextColor = ticker.Direction == PriceDirection.Up
            ? Gainer
            : ticker.Direction == PriceDirection.Down
                ? Loser
                : DefaultText;
    }

    private void UpdatePct(Ticker ticker)
    {
        PctLabel.Text = Formatting.FormatPct(ticker.Price24hPcnt);
        PctLabel.TextColor = ticker.Price24hPcnt >= 0 ? Gainer : Loser;
        PctBadge.BackgroundColor = ticker.Price24hPcnt >= 0 ? PctUpBg : PctDownBg;
    }

    private void Flash(PriceDirection direction)
    {
        if (direction == PriceDirection.Flat)
        {
            ResetFlash();
            return;
        }

        _flashCts?.Cancel();
        _flashCts = new CancellationTokenSource();
        var token = _flashCts.Token;
        BackgroundColor = direction == PriceDirection.Up ? FlashUpBg : FlashDownBg;
        _ = ResetFlashAfterDelayAsync(token);
    }

    private async Task ResetFlashAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);
            BackgroundColor = Colors.Transparent;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ResetFlash()
    {
        _flashCts?.Cancel();
        _flashCts?.Dispose();
        _flashCts = null;
        BackgroundColor = Colors.Transparent;
    }

    private void Unsubscribe()
    {
        if (_ticker is not null)
            _ticker.PropertyChanged -= OnTickerPropertyChanged;
        _ticker = null;
    }
}