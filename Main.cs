using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;
using BrowserInfo = Wox.Plugin.Common.DefaultBrowserInfo;
using System.Windows.Input;

namespace Community.PowerToys.Run.Plugin.AnagramSolver
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISettingProvider, IReloadable, IDisposable, IDelayedExecutionPlugin
    {
        private const string Setting = nameof(Setting);

        // current value of the setting
        private bool _setting;

        private PluginInitContext _context;

        private string _iconPath;

        private bool _disposed;

        public string Name => Properties.Resources.plugin_name;

        public string Description => Properties.Resources.plugin_description;

        // TODO: remove dash from ID below and inside plugin.json
        public static string PluginID => "f4f926c0-092e-48b2-aa21-e8e1e4d1e4c5";

        // TODO: add additional options (optional)
        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                PluginOptionType= PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Key = Setting,
                DisplayLabel = Properties.Resources.plugin_setting,
            },
        };

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            _setting = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == Setting)?.Value ?? false;
        }

        // TODO: return context menus for each Result (optional)
        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (selectedResult.Title == Properties.Resources.plugin_name)
            {
                return new List<ContextMenuResult>(0);
            }

            return [
                new() {
                    PluginName = Properties.Resources.plugin_name,
                    Title = "Open definition in browser (Ctrl + D)",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Glyph = "\xe82d",
                    AcceleratorKey = System.Windows.Input.Key.D,
                    AcceleratorModifiers = ModifierKeys.Control,
                    Action = _ => {
                        if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, $"https://en.wiktionary.org/wiki/{selectedResult.Title}")) {
                            return false;
                        }
                        return true;
                    }
                }
                ];
        }

        // TODO: return query results
        public List<Result> Query(Query query)
        {
            ArgumentNullException.ThrowIfNull(query);

            Log.Info("Query: " + query.Search, GetType());

            var results = new List<Result>();

            // empty query
            if (string.IsNullOrEmpty(query.Search))
            {
                results.Add(new Result
                {
                    Title = Properties.Resources.plugin_name,
                    SubTitle = "paste the scrambled word to unscramble",
                    // QueryTextDisplay = string.Empty,
                    IcoPath = _iconPath,
                    Action = action =>
                    {
                        return true;
                    },
                });
                return results;
            }
            else
            {
                var task = retrieve(query.Search);
                task.Wait();
                results.AddRange(task.Result);
            }

            return results;
        }

        public class WordList
        {
            public required string word { get; set; }
            public int score { get; set; }
        }


        public async Task<List<Result>> retrieve(string search)
        {

            var results = new List<Result>();
            var json = "";
            using(var client = new HttpClient())
            {
                var response = await client.GetAsync($"https://api.datamuse.com/words?sp=//{search}");
                json = await response.Content.ReadAsStringAsync();
            }
            var result = JsonSerializer.Deserialize<List<WordList>>(json);
            bool isEmpty = true;

            foreach (var item in result)
            {

                results.Add(new Result
                {
                    Title = item.word,
                    SubTitle = "press enter to copy" + " | score: " + item.score,
                    IcoPath = _iconPath,
                    Action = Action =>
                    {
                        try
                        {
                            Clipboard.SetText(item.word);
                            return true;
                        }
                        catch (Exception e)
                        {
                            Log.Exception("Copy failed", e, GetType());
                        }
                        return false;
                    }
                });

                isEmpty = false;
            }

            if (isEmpty)
            {
                results.Add(new Result
                {
                    Title = "Could not solve anagram",
                    IcoPath = _iconPath,
                    Action = _ =>
                    {
                        return true;
                    }
                });
            }

            return results;
        }

        // TODO: return delayed query results (optional)
        public List<Result> Query(Query query, bool delayedExecution)
        {
            ArgumentNullException.ThrowIfNull(query);

            var results = new List<Result>();

            // empty query
            if (string.IsNullOrEmpty(query.Search))
            {
                return results;
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.plugin_description;
        }

        private void OnThemeChanged(Theme oldTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _iconPath = "Images/AnagramSolver.light.png";
            }
            else
            {
                _iconPath = "Images/AnagramSolver.dark.png";
            }
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void ReloadData()
        {
            if (_context is null)
            {
                return;
            }

            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_context != null && _context.API != null)
                {
                    _context.API.ThemeChanged -= OnThemeChanged;
                }

                _disposed = true;
            }
        }
    }
}
