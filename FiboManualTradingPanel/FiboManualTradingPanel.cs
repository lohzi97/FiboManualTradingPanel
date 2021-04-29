using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class FiboManualTradingPanel : Robot
    {
        [Parameter("Vertical Position", Group = "Panel alignment", DefaultValue = VerticalAlignment.Top)]
        public VerticalAlignment PanelVerticalAlignment { get; set; }

        [Parameter("Horizontal Position", Group = "Panel alignment", DefaultValue = HorizontalAlignment.Left)]
        public HorizontalAlignment PanelHorizontalAlignment { get; set; }

        [Parameter("Default Stop Loss Level", Group = "Default trade parameters", DefaultValue = 0.0)]
        public double DefaultStopLossLevel { get; set; }

        [Parameter("Default Take Profit Level", Group = "Default trade parameters", DefaultValue = 2.0)]
        public double DefaultTakeProfitLevel { get; set; }

        [Parameter("Default Entry Level", Group = "Default trade parameters", DefaultValue = 0.5)]
        public double DefaultEntryLevel { get; set; }

        [Parameter("Delta", Group = "Risk Management", DefaultValue = 50)]
        public double Delta { get; set; }

        [Parameter("Risk Percentage", Group = "Risk Management", DefaultValue = 0.02)]
        public double RiskPercentage { get; set; }

        [Parameter("Max Drawdown (Base Currency)", Group = "Risk Management", DefaultValue = 10)]
        public double MaxDrawdown { get; set; }

        protected override void OnStart()
        {
            var tradingPanel = new TradingPanel(this, DefaultEntryLevel, DefaultStopLossLevel, DefaultTakeProfitLevel, Delta, RiskPercentage, MaxDrawdown);

            var border = new Border 
            {
                VerticalAlignment = PanelVerticalAlignment,
                HorizontalAlignment = PanelHorizontalAlignment,
                Style = Styles.CreatePanelBackgroundStyle(),
                Margin = "20 40 20 20",
                Width = 225,
                Child = tradingPanel
            };

            Chart.AddControl(border);
        }
    }

    public class TradingPanel : CustomControl
    {
        private const string EntryLevelInputKey = "EntryLevelKey";
        private const string TakeProfitLevelInputKey = "TakeProfitLevelKey";
        private const string StopLossLevelInputKey = "StopLossLevelKey";
        private readonly IDictionary<string, TextBox> _inputMap = new Dictionary<string, TextBox>();
        private readonly Robot _robot;
        private double _delta;
        private double _riskPercentage;
        private double _maxDrawdown;

        public TradingPanel(Robot robot, double defaultEntryLevel, double defaultStopLossLevel, double defaultTakeProfitLevel, double delta, double riskPercentage, double maxDrawdown)
        {
            _robot = robot;
            _delta = delta;
            _riskPercentage = riskPercentage;
            _maxDrawdown = maxDrawdown;
            AddChild(CreateTradingPanel(defaultEntryLevel, defaultStopLossLevel, defaultTakeProfitLevel));
        }

        private ControlBase CreateTradingPanel(double defaultEntryLevel, double defaultStopLossLevel, double defaultTakeProfitLevel)
        {
            var mainPanel = new StackPanel();

            var header = CreateHeader();
            mainPanel.AddChild(header);

            var contentPanel = CreateContentPanel(defaultEntryLevel, defaultStopLossLevel, defaultTakeProfitLevel);
            mainPanel.AddChild(contentPanel);

            return mainPanel;
        }

        private ControlBase CreateHeader()
        {
            var headerBorder = new Border 
            {
                BorderThickness = "0 0 0 1",
                Style = Styles.CreateCommonBorderStyle()
            };

            var header = new TextBlock 
            {
                Text = "Quick Trading Panel",
                Margin = "10 7",
                Style = Styles.CreateHeaderStyle()
            };

            headerBorder.Child = header;
            return headerBorder;
        }

        private StackPanel CreateContentPanel(double defaultLots, double defaultStopLossPips, double defaultTakeProfitPips)
        {
            var contentPanel = new StackPanel 
            {
                Margin = 10
            };
            var grid = new Grid(4, 3);
            grid.Columns[1].SetWidthInPixels(5);

            var sellButton = CreateTradeButton("SELL", Styles.CreateSellButtonStyle(), TradeType.Sell);
            grid.AddChild(sellButton, 0, 0);

            var buyButton = CreateTradeButton("BUY", Styles.CreateBuyButtonStyle(), TradeType.Buy);
            grid.AddChild(buyButton, 0, 2);

            var lotsInput = CreateInputWithLabel("Entry Level", defaultLots.ToString("F2"), EntryLevelInputKey);
            grid.AddChild(lotsInput, 1, 0, 1, 3);

            var stopLossInput = CreateInputWithLabel("Stop Loss Level", defaultStopLossPips.ToString("F1"), StopLossLevelInputKey);
            grid.AddChild(stopLossInput, 2, 0, 1, 3);

            var takeProfitInput = CreateInputWithLabel("Take Profit Level", defaultTakeProfitPips.ToString("F1"), TakeProfitLevelInputKey);
            grid.AddChild(takeProfitInput, 3, 0, 1, 3);

            contentPanel.AddChild(grid);

            return contentPanel;
        }

        private Button CreateTradeButton(string text, Style style, TradeType tradeType)
        {
            var tradeButton = new Button 
            {
                Text = text,
                Style = style,
                Height = 25
            };

            tradeButton.Click += args => PlaceOrderAsync(tradeType);

            return tradeButton;
        }

        private Panel CreateInputWithLabel(string label, string defaultValue, string inputKey)
        {
            var stackPanel = new StackPanel 
            {
                Orientation = Orientation.Vertical,
                Margin = "0 10 0 0"
            };

            var textBlock = new TextBlock 
            {
                Text = label
            };

            var input = new TextBox 
            {
                Margin = "0 5 0 0",
                Text = defaultValue,
                Style = Styles.CreateInputStyle()
            };

            _inputMap.Add(inputKey, input);

            stackPanel.AddChild(textBlock);
            stackPanel.AddChild(input);

            return stackPanel;
        }

        private void PlaceOrderAsync(TradeType tradeType)
        {
            // read the input from user
            double entryFiboLevel = GetValueFromInput(EntryLevelInputKey, 0);
            double stopLossFiboLevel = GetValueFromInput(StopLossLevelInputKey, 0);
            double takeProfitFiboLevel = GetValueFromInput(TakeProfitLevelInputKey, 0);

            // verify the input
            if (!VerifyInput(entryFiboLevel, stopLossFiboLevel, takeProfitFiboLevel))
            {
                _robot.Print("Invalid Fibo Level Input!");
                return;
            }

            double zeroFiboPrice;
            double hundredFiboPrice;

            if (tradeType == TradeType.Buy)
            {
                zeroFiboPrice = _robot.Bars.LowPrices.Last(1);
                hundredFiboPrice = _robot.Bars.HighPrices.Last(1);
            }
            // TradeType.Sell
            else
            {
                zeroFiboPrice = _robot.Bars.HighPrices.Last(1);
                hundredFiboPrice = _robot.Bars.LowPrices.Last(1);
            }

            double entryLevelPrice = FiboLevelToPrice(entryFiboLevel, zeroFiboPrice, hundredFiboPrice);
            double stopLossLevelPrice = FiboLevelToPrice(stopLossFiboLevel, zeroFiboPrice, hundredFiboPrice);
            double takeProfitLevelPrice = FiboLevelToPrice(takeProfitFiboLevel, zeroFiboPrice, hundredFiboPrice);

            double stopLossPips = Math.Abs(entryLevelPrice - stopLossLevelPrice) / _robot.Symbol.PipSize;
            double takeProfitPips = Math.Abs(takeProfitLevelPrice - entryLevelPrice) / _robot.Symbol.PipSize;

            double? lots = GetFixedRatioLots();
            if (!lots.HasValue)
            {
                _robot.Print("GetFixedRatioLots calculation exeed 10000 loops!");
                return;
            }
            else if (lots == 0)
            {
                _robot.Print("Insufficient account balance to place trade!");
            }
            double volume = _robot.Symbol.NormalizeVolumeInUnits(_robot.Symbol.QuantityToVolumeInUnits(lots.Value));

            DateTime expiry = _robot.Server.Time.AddMinutes(TimeFrameConverter.TimeFrame2Minutes(_robot.TimeFrame));

            _robot.PlaceLimitOrderAsync(tradeType, _robot.Symbol.Name, volume, entryLevelPrice, "FiboManualTradingPanel", stopLossPips, takeProfitPips, expiry);
        }

        private bool VerifyInput(double entryLevel, double stopLossLevel, double takeProfitLevel)
        {
            if (stopLossLevel >= entryLevel || stopLossLevel >= takeProfitLevel || entryLevel >= takeProfitLevel)
                return false;
            else
                return true;
        }

        private double FiboLevelToPrice(double fiboLevel, double zeroLevelPrice, double hundredLevelPrice)
        {
            return (hundredLevelPrice - zeroLevelPrice) * fiboLevel + zeroLevelPrice;
        }

        private double? GetFixedRatioLots()
        {
            double currentBalance = _robot.Account.Balance;
            double minRequiredBalance = _maxDrawdown / _riskPercentage;
            if (currentBalance < minRequiredBalance)
                return 0;

            double cummulativeBalance = minRequiredBalance;
            double lots = 0.01;
            //use a for loop to avoid infinite loop issue
            for (int i = 0; i < 10000; i++)
            {
                cummulativeBalance = cummulativeBalance + lots * 100 * _delta;
                if (cummulativeBalance > currentBalance)
                    return lots;
                lots += 0.01;
            }
            return null;
        }

        private double GetValueFromInput(string inputKey, double defaultValue)
        {
            double value;

            return double.TryParse(_inputMap[inputKey].Text, out value) ? value : defaultValue;
        }
    }

    public static class Styles
    {
        public static Style CreatePanelBackgroundStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.CornerRadius, 3);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#292929"), 0.85m), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.85m), ControlState.LightTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#3C3C3C"), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#C3C3C3"), ControlState.LightTheme);
            style.Set(ControlProperty.BorderThickness, new Thickness(1));

            return style;
        }

        public static Style CreateCommonBorderStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.BorderColor, GetColorWithOpacity(Color.FromHex("#FFFFFF"), 0.12m), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, GetColorWithOpacity(Color.FromHex("#000000"), 0.12m), ControlState.LightTheme);
            return style;
        }

        public static Style CreateHeaderStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.ForegroundColor, GetColorWithOpacity("#FFFFFF", 0.70m), ControlState.DarkTheme);
            style.Set(ControlProperty.ForegroundColor, GetColorWithOpacity("#000000", 0.65m), ControlState.LightTheme);
            return style;
        }

        public static Style CreateInputStyle()
        {
            var style = new Style(DefaultStyles.TextBoxStyle);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#1A1A1A"), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#111111"), ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#E7EBED"), ControlState.LightTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#D6DADC"), ControlState.LightTheme | ControlState.Hover);
            style.Set(ControlProperty.CornerRadius, 3);
            return style;
        }

        public static Style CreateBuyButtonStyle()
        {
            return CreateButtonStyle(Color.FromHex("#009345"), Color.FromHex("#10A651"));
        }

        public static Style CreateSellButtonStyle()
        {
            return CreateButtonStyle(Color.FromHex("#F05824"), Color.FromHex("#FF6C36"));
        }

        public static Style CreateCloseButtonStyle()
        {
            return CreateButtonStyle(Color.FromHex("#F05824"), Color.FromHex("#FF6C36"));
        }

        private static Style CreateButtonStyle(Color color, Color hoverColor)
        {
            var style = new Style(DefaultStyles.ButtonStyle);
            style.Set(ControlProperty.BackgroundColor, color, ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, color, ControlState.LightTheme);
            style.Set(ControlProperty.BackgroundColor, hoverColor, ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, hoverColor, ControlState.LightTheme | ControlState.Hover);
            style.Set(ControlProperty.ForegroundColor, Color.FromHex("#FFFFFF"), ControlState.DarkTheme);
            style.Set(ControlProperty.ForegroundColor, Color.FromHex("#FFFFFF"), ControlState.LightTheme);
            return style;
        }

        private static Color GetColorWithOpacity(Color baseColor, decimal opacity)
        {
            var alpha = (int)Math.Round(byte.MaxValue * opacity, MidpointRounding.AwayFromZero);
            return Color.FromArgb(alpha, baseColor);
        }
    }

    public static class TimeFrameConverter
    {
        public static double TimeFrame2Minutes(TimeFrame timeframe)
        {
            if (timeframe == TimeFrame.Daily)
                return new TimeSpan(1, 0, 0, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Day2)
                return new TimeSpan(2, 0, 0, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Day3)
                return new TimeSpan(3, 0, 0, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Hour)
                return new TimeSpan(1, 0, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Hour12)
                return new TimeSpan(12, 0, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Hour2)
                return new TimeSpan(2, 0, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Hour3)
                return new TimeSpan(3, 0, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Hour4)
                return new TimeSpan(4, 0, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Hour6)
                return new TimeSpan(6, 0, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Hour8)
                return new TimeSpan(8, 0, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute)
                return new TimeSpan(0, 1, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute10)
                return new TimeSpan(0, 10, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute15)
                return new TimeSpan(0, 15, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute2)
                return new TimeSpan(0, 2, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute20)
                return new TimeSpan(0, 20, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute3)
                return new TimeSpan(0, 3, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute30)
                return new TimeSpan(0, 30, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute4)
                return new TimeSpan(0, 4, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute45)
                return new TimeSpan(0, 45, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute5)
                return new TimeSpan(0, 5, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute6)
                return new TimeSpan(0, 6, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute7)
                return new TimeSpan(0, 7, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute8)
                return new TimeSpan(0, 8, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Minute9)
                return new TimeSpan(0, 9, 0).TotalMinutes;
            else if (timeframe == TimeFrame.Monthly)
                return new TimeSpan(30, 0, 0, 0).TotalMinutes;
            else
                throw new ArgumentException("Invalid Timeframe: " + timeframe.ToString());
        }
    }

}
