﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommonMark.Syntax;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using MarkdownEdit.Commands;
using MarkdownEdit.Models;
using MarkdownEdit.Snippets;
using MarkdownEdit.SpellCheck;
using static MarkdownEdit.i18n.TranslationProvider;
using static MarkdownEdit.Models.AbstractSyntaxTree;

namespace MarkdownEdit.Controls
{
    public partial class Editor : INotifyPropertyChanged
    {
        public static readonly DependencyProperty AutoSaveProperty = DependencyProperty.Register(
            "AutoSave", typeof(bool), typeof(Editor), new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
            "Theme", typeof(Theme), typeof(Editor), new PropertyMetadata(default(Theme), ThemeChangedCallback));

        public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = DependencyProperty.Register(
            "VerticalScrollBarVisibility", typeof(ScrollBarVisibility), typeof(Editor),
            new PropertyMetadata(default(ScrollBarVisibility)));

        public static readonly DependencyProperty ShowEndOfLineProperty = DependencyProperty.Register(
            "ShowEndOfLine", typeof(bool), typeof(Editor), new PropertyMetadata(default(bool), ShowEndOfLineChanged));

        public static readonly DependencyProperty ShowSpacesProperty = DependencyProperty.Register(
            "ShowSpaces", typeof(bool), typeof(Editor), new PropertyMetadata(default(bool), ShowSpacesChanged));

        public static readonly DependencyProperty ShowLineNumbersProperty = DependencyProperty.Register(
            "ShowLineNumbers", typeof(bool), typeof(Editor), new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty ShowTabsProperty = DependencyProperty.Register(
            "ShowTabs", typeof(bool), typeof(Editor), new PropertyMetadata(default(bool), ShowTabsChanged));

        public static readonly DependencyProperty SpellCheckProviderProperty = DependencyProperty.Register(
            "SpellCheckProvider", typeof(ISpellCheckProvider), typeof(Editor),
            new PropertyMetadata(default(ISpellCheckProvider), SpellCheckProviderPropertyChanged));

        public static readonly DependencyProperty HighlightCurrentLineProperty = DependencyProperty.Register(
            "HighlightCurrentLine", typeof(bool), typeof(Editor),
            new PropertyMetadata(default(bool), HighlightCurrentLineChanged));

        public static readonly DependencyProperty SnippetManagerProperty = DependencyProperty.Register(
            "SnippetManager", typeof(ISnippetManager), typeof(Editor), new PropertyMetadata(default(ISnippetManager)));

        public static readonly DependencyProperty WordWrapProperty = DependencyProperty.Register(
            "WordWrap", typeof(bool), typeof(Editor), new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty SpellCheckProperty = DependencyProperty.Register(
            "SpellCheck", typeof(bool), typeof(Editor), new PropertyMetadata(default(bool), SpellCheckPropertyChanged));

        public static readonly DependencyProperty AbstractSyntaxTreeProperty = DependencyProperty.Register(
            "AbstractSyntaxTree", typeof(Block), typeof(Editor), new PropertyMetadata(default(Block)));

        public static readonly DependencyProperty MyEncodingInfoProperty = DependencyProperty.Register(
            "Encoding", typeof(MyEncodingInfo), typeof(Editor),
            new PropertyMetadata(default(MyEncodingInfo), MyEncodingPropertyChanged));

        private readonly string _f1ForHelp = (string)Translate("editor-f1-for-help");
        private string _displayName = string.Empty;
        private EditorState _editorState = new EditorState();
        private string _fileName;
        private FindReplaceDialog _findReplaceDialog;
        private bool _isModified;
        public bool RemoveSpecialCharacters;
        private int _previousLineCount = -1;

        public EventHandler TextChanged;
        public EventHandler<ThemeChangedEventArgs> ThemeChanged;

        public Editor()
        {
            InitializeComponent();
            SetupSyntaxHighlighting(); // won't paint on first load unless here.
            IsVisibleChanged += OnIsVisibleChanged;
        }

        public FindReplaceDialog FindReplaceDialog
            => _findReplaceDialog ?? (_findReplaceDialog = new FindReplaceDialog(new FindReplaceSettings()));

        public string Text
        {
            get => EditBox.Text;
            set => EditBox.Text = value;
        }

        public bool IsReadOnly
        {
            get => EditBox.IsReadOnly;
            set => EditBox.IsReadOnly = value;
        }

        public string FileName
        {
            get => _fileName;
            set => Set(ref _fileName, value);
        }

        public string DisplayName
        {
            get => string.IsNullOrWhiteSpace(_displayName) == false
                ? _displayName
                : string.IsNullOrWhiteSpace(FileName)
                    ? $"{Translate("editor-new-document")} {_f1ForHelp}"
                    : Path.GetFileName(FileName);
            set => Set(ref _displayName, value);
        }

        public bool IsModified
        {
            get => _isModified;
            set => Set(ref _isModified, value);
        }

        public bool AutoSave
        {
            get => (bool)GetValue(AutoSaveProperty);
            set => SetValue(AutoSaveProperty, value);
        }

        public Theme Theme
        {
            get => (Theme)GetValue(ThemeProperty);
            set => SetValue(ThemeProperty, value);
        }

        public ScrollBarVisibility VerticalScrollBarVisibility
        {
            get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
            set => SetValue(VerticalScrollBarVisibilityProperty, value);
        }

        public bool ShowEndOfLine
        {
            get => (bool)GetValue(ShowEndOfLineProperty);
            set => SetValue(ShowEndOfLineProperty, value);
        }

        public bool ShowSpaces
        {
            get => (bool)GetValue(ShowSpacesProperty);
            set => SetValue(ShowSpacesProperty, value);
        }

        public bool ShowLineNumbers
        {
            get => (bool)GetValue(ShowLineNumbersProperty);
            set => SetValue(ShowLineNumbersProperty, value);
        }

        public bool ShowTabs
        {
            get => (bool)GetValue(ShowTabsProperty);
            set => SetValue(ShowTabsProperty, value);
        }

        public ISpellCheckProvider SpellCheckProvider
        {
            get => (ISpellCheckProvider)GetValue(SpellCheckProviderProperty);
            set => SetValue(SpellCheckProviderProperty, value);
        }

        public bool HighlightCurrentLine
        {
            get => (bool)GetValue(HighlightCurrentLineProperty);
            set => SetValue(HighlightCurrentLineProperty, value);
        }

        public ISnippetManager SnippetManager
        {
            get => (ISnippetManager)GetValue(SnippetManagerProperty);
            set => SetValue(SnippetManagerProperty, value);
        }

        public bool WordWrap
        {
            get => (bool)GetValue(WordWrapProperty);
            set => SetValue(WordWrapProperty, value);
        }

        public bool SpellCheck
        {
            get => (bool)GetValue(SpellCheckProperty);
            set => SetValue(SpellCheckProperty, value);
        }

        public Block AbstractSyntaxTree
        {
            get => (Block)GetValue(AbstractSyntaxTreeProperty);
            set => SetValue(AbstractSyntaxTreeProperty, value);
        }

        public MyEncodingInfo Encoding
        {
            get => (MyEncodingInfo)GetValue(MyEncodingInfoProperty);
            set => SetValue(MyEncodingInfoProperty, value);
        }

        private void OnIsVisibleChanged(object sender,
            DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            IsVisibleChanged -= OnIsVisibleChanged;

            EditBox.Options.IndentationSize = 2;
            EditBox.Options.EnableHyperlinks = false;
            EditBox.Options.ConvertTabsToSpaces = true;
            EditBox.Options.AllowScrollBelowDocument = true;
            EditBox.Options.EnableHyperlinks = false;
            EditBox.Options.EnableEmailHyperlinks = false;
            EditBox.TextChanged += EditBoxOnTextChanged;

            Dispatcher.InvokeAsync(() =>
            {
                var executeAutoSave = Utility.Debounce<string>(s => Dispatcher?.Invoke(ExecuteAutoSave), 4000);
                EditBox.TextChanged += (s, e) => executeAutoSave(null);
                EditBox.PreviewKeyDown += (s, e) => EditorSpellCheck.AppsKeyDown = e.Key == Key.Apps && e.IsDown;
                EditBox.Document.PropertyChanged += OnEditBoxPropertyChanged;
                DataObject.AddPastingHandler(EditBox, OnPaste);
                RestyleScrollBar();
                EditorUtilities.AllowImagePaste(this);
                RemoveIndentationCommandBinding();
                SetupTabSnippetHandler();
                ThemeChangedCallback(this, new DependencyPropertyChangedEventArgs());
                EditBox.Focus();

                // fixes context menu not showing on first click
                ContextMenu = new ContextMenu();
                ContextMenu.Items.Add(new MenuItem());
            });
        }

        private void SetupSyntaxHighlighting()
        {
            var colorizer = new MarkdownHighlightingColorizer();
            var blockBackgroundRenderer = new BlockBackgroundRenderer();

            TextChanged += (s, e) =>
            {
                try
                {
                    AbstractSyntaxTree = GenerateAbstractSyntaxTree(Text);
                    colorizer.UpdateAbstractSyntaxTree(AbstractSyntaxTree);
                    blockBackgroundRenderer.UpdateAbstractSyntaxTree(AbstractSyntaxTree);
                    // The block nature of markdown causes edge cases in the syntax hightlighting.
                    // This is the nuclear option but it doesn't seem to cause issues.
                    EditBox.TextArea.TextView.Redraw();
                }
                catch (Exception ex)
                {
                    // See #159
                    Notify.Alert($"Abstract Syntax Tree generation failed: {ex.ToString()}");
                }
            };

            ThemeChanged += (s, e) =>
            {
                colorizer.OnThemeChanged(e.Theme);
                blockBackgroundRenderer.OnThemeChanged(e.Theme);
            };

            EditBox.TextArea.TextView.LineTransformers.Add(colorizer);
            EditBox.TextArea.TextView.BackgroundRenderers.Add(blockBackgroundRenderer);
        }

        private void OnEditBoxPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == "LineCount")
            {

                var currentLineCount = EditBox.LineCount;
                // Magic number, yeah baby! -1 means the editor has never had text in it.
                if (_previousLineCount != -1)
                {
                    ListInputHandler.AdjustList(EditBox, currentLineCount > _previousLineCount);
                }
                _previousLineCount = currentLineCount;
            }
        }

        private double GetScrollMax()
        {
            var scrollViewer = EditBox.GetDescendantByType<ScrollViewer>();
            return scrollViewer.ScrollableHeight;
        }

        private void RestyleScrollBar()
        {
            // make scroll bar narrower
            var grid = EditBox.GetDescendantByType<Grid>();
            grid.ColumnDefinitions[1].Width = new GridLength(8);
            grid.RowDefinitions[1].Height = new GridLength(8);
            grid.Children[1].Opacity = 0.3;
            grid.Children[2].Opacity = 0.3;
        }

        private void RemoveIndentationCommandBinding()
        {
            var cmd = EditBox
                .TextArea
                .DefaultInputHandler
                .Editing
                .CommandBindings
                .FirstOrDefault(cb => cb.Command == AvalonEditCommands.IndentSelection);
            if (cmd != null) EditBox.TextArea.DefaultInputHandler.Editing.CommandBindings.Remove(cmd);
        }

        private void SetupTabSnippetHandler()
        {
            var editingKeyBindings = EditBox.TextArea.DefaultInputHandler.Editing.InputBindings.OfType<KeyBinding>();
            var tabBinding = editingKeyBindings.Single(b => b.Key == Key.Tab && b.Modifiers == ModifierKeys.None);
            EditBox.TextArea.DefaultInputHandler.Editing.InputBindings.Remove(tabBinding);
            var newTabBinding = new KeyBinding(new SnippetTabCommand(EditBox, tabBinding.Command, SnippetManager),
                tabBinding.Key, tabBinding.Modifiers);
            EditBox.TextArea.DefaultInputHandler.Editing.InputBindings.Add(newTabBinding);
            SnippetManager.Initialize();
        }

        public Tuple<int, int> VisibleBlockNumber()
        {
            if (AbstractSyntaxTree == null) return new Tuple<int, int>(1, 0);
            var textView = EditBox.TextArea.TextView;

            var max = GetScrollMax();
            if (textView.ScrollOffset.Y >= max) return new Tuple<int, int>(int.MaxValue, 0);

            var line = textView.GetDocumentLineByVisualTop(textView.ScrollOffset.Y);
            var number = 1;
            var blockOffset = line.Offset;
            var skipListItem = true;

            foreach (var block in EnumerateBlocks(AbstractSyntaxTree.FirstChild))
            {
                if (block.Tag == BlockTag.List) skipListItem = block.ListData.IsTight;
                blockOffset = block.SourcePosition;
                if (block.SourcePosition >= line.Offset) break;
                if (block.Tag == BlockTag.ListItem && skipListItem) continue;
                number += 1;
            }

            var startOfBlock = EditBox.Document.GetLineByOffset(blockOffset);
            var extra = line.LineNumber - startOfBlock.LineNumber;
            return new Tuple<int, int>(number, extra);
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs pasteEventArgs)
        {
            var text = (string)pasteEventArgs.SourceDataObject.GetData(DataFormats.UnicodeText, true);
            if (string.IsNullOrWhiteSpace(text)) return;
            if (RemoveSpecialCharacters)
            {
                text = text.ReplaceSmartChars();
            }
            else if (Uri.IsWellFormedUriString(text, UriKind.Absolute)
                     && PositionSafeForSmartLink(AbstractSyntaxTree, EditBox.SelectionStart, EditBox.SelectionLength))
            {
                text = Images.IsImageUrl(text.TrimEnd())
                    ? $"![{EditBox.SelectedText}]({text})\n"
                    : string.IsNullOrEmpty(EditBox.SelectedText) ? $"<{text}>" : $"[{EditBox.SelectedText}]({text})";
            }
            else
            {
                return;
            }

            var dataObject = new DataObject();
            dataObject.SetData(DataFormats.UnicodeText, text);
            pasteEventArgs.DataObject = dataObject;
        }

        protected override void OnDragEnter(DragEventArgs dea)
        {
            if (dea.Data.GetDataPresent(DataFormats.FileDrop) == false) dea.Effects = DragDropEffects.None;
        }

        protected override void OnDrop(DragEventArgs dea)
        {
            if (dea.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = dea.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null) return;

                if (Images.HasImageExtension(files[0]))
                {
                    var dialog = new ImageDropDialog
                    {
                        Owner = Application.Current.MainWindow,
                        TextEditor = EditBox,
                        DocumentFileName = FileName,
                        DragEventArgs = dea
                    };
                    dialog.ShowDialog();
                }
                else
                {
                    Dispatcher.InvokeAsync(() => OpenFileCommand.Command.Execute(files[0], this));
                }
            }
        }

        private void ExecuteSpellCheckReplace(object sender, ExecutedRoutedEventArgs ea)
        {
            var parameters = (Tuple<string, TextSegment>)ea.Parameter;
            var word = parameters.Item1;
            var segment = parameters.Item2;
            EditBox.Document.Replace(segment, word);
        }

        private void ExecuteAddToDictionary(object sender, ExecutedRoutedEventArgs ea)
        {
            var word = (string)ea.Parameter;
            SpellCheckProvider.Add(word);
            SpellCheck = !SpellCheck;
            SpellCheck = !SpellCheck;
        }

        private void CanExecute(object sender, CanExecuteRoutedEventArgs e) { e.CanExecute = !EditBox.IsReadOnly; }

        public void FormatTextHandler(Func<string, string> converter, bool? forceAllText)
        {
            var isSelectedText = !string.IsNullOrEmpty(EditBox.SelectedText) && !forceAllText.GetValueOrDefault(false);
            var originalText = isSelectedText ? EditBox.SelectedText : EditBox.Document.Text;
            var formattedText = converter(originalText);
            if (string.CompareOrdinal(formattedText, originalText) == 0) return;
            if (isSelectedText)
            {
                EditBox.SelectedText = formattedText;
            }
            else
            {
                var start = EditBox.SelectionStart;
                EditBox.Document.Text = formattedText;
                EditBox.SelectionStart = Math.Min(start, formattedText.Length);
            }
        }

        public bool SaveIfModified()
            => EditorLoadSave.SaveIfModified(this);

        public bool SaveFile()
            => EditorLoadSave.SaveFile(this);

        public bool SaveFileAs()
            => EditorLoadSave.SaveFileAs(this);

        public bool LoadFile(string file, bool updateCursorPosition = true)
            => EditorLoadSave.LoadFile(this, file, updateCursorPosition);

        public void ExecuteAutoSave()
        {
            if (AutoSave == false || IsModified == false || string.IsNullOrEmpty(FileName)) return;
            {
                if (AutoSave == false || IsModified == false || string.IsNullOrEmpty(FileName)) return;
                SaveFile();
            }
        }

        public void ToggleHelp()
        {
            if (_editorState.StateSaved)
            {
                _editorState.Restore(this);
                ExecuteAutoSave();
                return;
            }
            _editorState.Save(this);
            Text = LoadHelp();
            EditBox.IsModified = false;
            DisplayName = "Help";
        }

        public void CloseHelp()
            => _editorState.Restore(this);

        private void ExecuteFindDialog(object sender, ExecutedRoutedEventArgs e)
            => FindReplaceDialog.ShowFindDialog();

        public void ReplaceDialog() => 
            FindReplaceDialog.ShowReplaceDialog();

        public void Bold() 
            => EditBox.AddRemoveText("**");

        public void Italic() 
            => EditBox.AddRemoveText("*");

        public void Code() 
            => EditBox.AddRemoveText("`");

        public void InsertHeader(int num)
        {
            var line = EditBox.Document.GetLineByOffset(EditBox.CaretOffset);
            if (line == null) return;
            var header = new string('#', num) + " ";
            EditBox.Document.Insert(line.Offset, header);
        }

        public void IncreaseFontSize() 
            => EditBox.FontSize = EditBox.FontSize + 1;

        public void DecreaseFontSize()
            => EditBox.FontSize = EditBox.FontSize > 5 ? EditBox.FontSize - 1 : EditBox.FontSize;

        public void RestoreFontSize() 
            => EditBox.FontSize = App.UserSettings.EditorFontSize;

        public void OpenUserDictionary() 
            => Utility.EditFile(SpellCheckProvider.CustomDictionaryFile());

        public void SelectPreviousHeader() 
            => EditBox.SelectHeader(false);

        public void SelectNextHeader() 
            => EditBox.SelectHeader(true);

        public bool Find(Regex find) 
            => EditBox.Find(find);

        public bool Replace(Regex find, string replace) 
            => EditBox.Replace(find, replace);

        public void ReplaceAll(Regex find, string replace) 
            => EditBox.ReplaceAll(find, replace);

        private void EditBoxOnTextChanged(object sender, EventArgs eventArgs) 
            => TextChanged?.Invoke(this, eventArgs);

        private void EditorMenuOnContextMenuOpening(object sender, ContextMenuEventArgs e)
            => EditorUtilities.EditorMenuOnContextMenuOpening(sender, e);

        public event ScrollChangedEventHandler ScrollChanged;

        private void ScrollViewerOnScrollChanged(object sender, ScrollChangedEventArgs ea)
            => ScrollChanged?.Invoke(this, ea);

        private void OnThemeChanged(ThemeChangedEventArgs ea) 
            => ThemeChanged?.Invoke(this, ea);

        public static void ThemeChangedCallback(DependencyObject source, DependencyPropertyChangedEventArgs ea)
        {
            var editor = (Editor)source;
            editor.OnThemeChanged(new ThemeChangedEventArgs { Theme = editor.Theme });
        }

        private static void ShowEndOfLineChanged(DependencyObject source, DependencyPropertyChangedEventArgs ea)
        {
            var editor = (Editor)source;
            editor.EditBox.Options.ShowEndOfLine = editor.ShowEndOfLine;
        }

        private static void ShowSpacesChanged(DependencyObject source, DependencyPropertyChangedEventArgs ea)
        {
            var editor = (Editor)source;
            editor.EditBox.Options.ShowSpaces = editor.ShowSpaces;
        }

        private static void ShowTabsChanged(DependencyObject source, DependencyPropertyChangedEventArgs ea)
        {
            var editor = (Editor)source;
            editor.EditBox.Options.ShowTabs = editor.ShowTabs;
        }

        private static void SpellCheckProviderPropertyChanged(DependencyObject source,
            DependencyPropertyChangedEventArgs e)
        {
            var editor = (Editor)source;
            editor.SpellCheckProvider.Initialize(editor);
            editor.SpellCheckProvider.Enabled = editor.SpellCheck;
        }

        private static void HighlightCurrentLineChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var editor = (Editor)source;
            editor.EditBox.Options.HighlightCurrentLine = editor.HighlightCurrentLine;
        }

        private static void SpellCheckPropertyChanged(DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs ea)
        {
            var editor = (Editor)dependencyObject;
            if (editor.SpellCheckProvider == null) return;
            editor.SpellCheckProvider.Enabled = (bool)ea.NewValue;
            editor.EditBox.Document.Insert(0, " ");
            editor.EditBox.Document.UndoStack.Undo();
        }

        private static void MyEncodingPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var editor = (Editor)source;
            var encoding = System.Text.Encoding.GetEncoding(editor.Encoding.CodePage);
            editor.EditBox.Encoding = encoding;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(property, value)) return;
            property = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}