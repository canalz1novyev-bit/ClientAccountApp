using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace ClientAccountApp
{
    public enum StatusKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    // Покажет сообщение в TextBlock, подсветит цветом и автоматически очистит через TTL.
    // Один таймер на каждый TextBlock, привязка через ConditionalWeakTable не нужна — TextBlock живёт со страницей.
    public static class StatusMessageHelper
    {
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ErrorTtl = TimeSpan.FromSeconds(8);

        private static readonly Dictionary<TextBlock, DispatcherQueueTimer> Timers = new();

        public static void Show(TextBlock? target, string text, StatusKind kind = StatusKind.Info, TimeSpan? ttl = null)
        {
            if (target == null) return;

            target.Text = text;
            target.Foreground = ResolveBrush(kind);

            var queue = target.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            if (queue == null) return;

            if (!Timers.TryGetValue(target, out var timer))
            {
                timer = queue.CreateTimer();
                timer.IsRepeating = false;
                timer.Tick += (s, _) => target.Text = "";
                Timers[target] = timer;
            }

            timer.Stop();
            if (string.IsNullOrEmpty(text)) return;

            timer.Interval = ttl ?? (kind == StatusKind.Error ? ErrorTtl : DefaultTtl);
            timer.Start();
        }

        public static void Info(TextBlock? target, string text) => Show(target, text, StatusKind.Info);
        public static void Success(TextBlock? target, string text) => Show(target, text, StatusKind.Success);
        public static void Warning(TextBlock? target, string text) => Show(target, text, StatusKind.Warning);
        public static void Error(TextBlock? target, string text) => Show(target, text, StatusKind.Error);

        private static Brush ResolveBrush(StatusKind kind)
        {
            string key = kind switch
            {
                StatusKind.Success => "NiatecSuccessBrush",
                StatusKind.Warning => "NiatecWarningBrush",
                StatusKind.Error => "NiatecDangerBrush",
                _ => "NiatecInfoBrush"
            };

            if (Application.Current?.Resources != null &&
                Application.Current.Resources.TryGetValue(key, out object value) &&
                value is Brush brush)
            {
                return brush;
            }

            return new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }
    }
}
