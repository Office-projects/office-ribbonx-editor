﻿using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OfficeRibbonXEditor.Models;
using OfficeRibbonXEditor.ViewModels;

using ScintillaNET;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace OfficeRibbonXEditor.Views
{
    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow
    {
        private MainWindowViewModel viewModel;

        private bool suppressRequestBringIntoView;

        private GridLength lastResultsHeight = new GridLength(150);

        public MainWindow()
        {
            this.InitializeComponent();

            this.ResultsPanel.Scintilla = this.Editor.Scintilla;
        }

        public int Zoom
        {
            get => this.Editor?.Zoom ?? 0;
            set
            {
                if (this.Editor == null || this.Editor.Zoom == value)
                {
                    return;
                }

                this.Editor.Zoom = value;
            }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs args)
        {
            base.OnPropertyChanged(args);

            if (args.Property != DataContextProperty || !(args.NewValue is MainWindowViewModel model))
            {
                return;
            }

            this.viewModel = model;

            this.viewModel.ReadEditorInfo += (o, e) => e.Data = new EditorInfo { Text = this.Editor.Text, Selection = Tuple.Create(this.Editor.SelectionStart, this.Editor.SelectionEnd) };
            this.viewModel.InsertRecentFile += (o, e) => this.RecentFileList.InsertFile(e.Data);
            this.viewModel.UpdateEditor += (o, e) =>
            {
                this.Editor.DeleteRange(e.Start, (e.End >= 0 ? e.End : this.Editor.TextLength) - e.Start);
                this.Editor.InsertText(e.Start, e.NewText);
                if (e.UpdateSelection)
                {
                    this.Editor.SetSelection(e.Start, e.Start + e.NewText.Length);
                }

                if (e.ResetUndoHistory)
                {
                    this.Editor.EmptyUndoBuffer();
                }
            };
            this.viewModel.ShowResults += (o, e) =>
            {
                this.ResultsSplitter.Visibility = Visibility.Visible;
                this.ResultsRow.Height = this.lastResultsHeight;
                this.ResultsHeader.Content = e.Data.Header;
                this.ResultsPanel.UpdateFindAllResults(e.Data);
            };
        }

        /// <summary>
        /// Finds the first TreeViewItem which is a parent of the given source.
        /// See <a href="https://stackoverflow.com/questions/592373/select-treeview-node-on-right-click-before-displaying-contextmenu">this</a>
        /// </summary>
        /// <param name="source">The starting source where the TreeViewItem will be searched</param>
        /// <returns>The item found, or null otherwise</returns>
        private static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
            {
                source = VisualTreeHelper.GetParent(source);
            }

            return source as TreeViewItem;
        }

        #region Disable horizontal scrolling when selecting TreeView item

        private void OnTreeViewItemRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Ignore re-entrant calls
            if (this.suppressRequestBringIntoView)
            {
                return;
            }

            // Cancel the current scroll attempt
            e.Handled = true;

            // Call BringIntoView using a rectangle that extends into "negative space" to the left of our
            // actual control. This allows the vertical scrolling behaviour to operate without adversely
            // affecting the current horizontal scroll position.
            this.suppressRequestBringIntoView = true;

            if (sender is TreeViewItem item)
            {
                var newTargetRect = new Rect(-1000, 0, item.ActualWidth + 1000, item.ActualHeight);
                item.BringIntoView(newTargetRect);
            }

            this.suppressRequestBringIntoView = false;
        }

        // The call to BringIntoView() in OnTreeViewItemSelected is also important
        private void OnTreeViewItemSelected(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;

            // Correctly handle horizontally scrolling for programmatically selected items
            item.BringIntoView();
            e.Handled = true;
        }

        #endregion

        private void OnDocumentViewSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var newItem = e.NewValue as TreeViewItemViewModel;

            this.viewModel.SelectedItem = newItem;

            if (newItem == null || !newItem.CanHaveContents)
            {
                this.Editor.Text = string.Empty;
            }
            else
            {
                this.Editor.Text = newItem.Contents;
            }

            // In any case, reset the undo history
            this.Editor.EmptyUndoBuffer();
        }

        private void OnScintillaUpdateUi(object sender, UpdateUIEventArgs e)
        {
            if (!this.Editor.IsEnabled)
            {
                this.LineBox.Text = "Ln 0, Col 0";
            }
            else
            {
                var pos = this.Editor.CurrentPosition;
                var line = this.Editor.LineFromPosition(pos);
                var col = this.Editor.GetColumn(pos);

                this.LineBox.Text = $"Ln {line + 1},  Col {col + 1}";
            }
        }

        private void OnEditorZoomChanged(object sender, EventArgs e)
        {
            this.Zoom = this.Editor.Zoom;
        }

        /// <summary>
        /// Ensures that a TreeViewItem is selected before the context menu is displayed
        /// See <a href="https://stackoverflow.com/questions/592373/select-treeview-node-on-right-click-before-displaying-contextmenu">this</a>
        /// </summary>
        /// <param name="sender">The sender of the right click</param>
        /// <param name="e">The event arguments</param>
        private void OnTreeViewRightClick(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        private void OnChangeIdTextDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox textBox))
            {
                return;
            }

            if (!(textBox.DataContext is IconViewModel icon))
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                icon.CommitIdChange();
            }
            else if (e.Key == Key.Escape)
            {
                icon.DiscardIdChange();
            }
        }

        private void OnIdTextVisible(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue == false)
            {
                return;
            }

            if (!(sender is TextBox textBox))
            {
                return;
            }

            textBox.Focus();
            textBox.SelectAll();
        }
        
        // For some reason, the Scintilla editor always seems to have preference over the input gestures.
        // The only solution (so far) is to execute those when appropriate from this event handler.
        private void OnEditorKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            foreach (var ib in this.InputBindings)
            {
                if (!(ib is KeyBinding kb))
                {
                    return;
                }
                        
                var inputKey = KeyInterop.KeyFromVirtualKey(e.KeyValue);
                if (kb.Key != inputKey)
                {
                    continue;
                }

                if (kb.Modifiers.HasFlag(ModifierKeys.Control) != e.Control)
                {
                    continue;
                }
                        
                if (kb.Modifiers.HasFlag(ModifierKeys.Shift) != e.Shift)
                {
                    continue;
                }
                        
                if (kb.Modifiers.HasFlag(ModifierKeys.Alt) != e.Alt)
                {
                    continue;
                }

                e.SuppressKeyPress = true;
                e.Handled = true;

                // If this is not async, the lines above seem to be ignored and the character is still printed when
                // launching a dialog (e.g. Ctrl-O). See: https://github.com/fernandreu/office-ribbonx-editor/issues/20
                this.Dispatcher.InvokeAsync(() => kb.Command.Execute(null));

                return;
            }
        }

        private void OnEditorInsertCheck(object sender, InsertCheckEventArgs e)
        {
            if (!Properties.Settings.Default.AutoIndent)
            {
                return;
            }

            if (!e.Text.EndsWith("\r") && !e.Text.EndsWith("\n"))
            {
                return;
            }

            var startPos = this.Editor.Lines[this.Editor.LineFromPosition(this.Editor.CurrentPosition)].Position;
            var endPos = e.Position;
            var curLineText = this.Editor.GetTextRange(startPos, endPos - startPos); // Text until the caret
            var indent = Regex.Match(curLineText, "^[ \\t]*");
            e.Text += indent.Value;
            if (Regex.IsMatch(curLineText, "[^/]>\\s*$"))
            {
                // If the previous line finished with an open tag, add an indentation level to the next one 
                e.Text += new string(' ', Properties.Settings.Default.TabWidth);
            }
        }

        private void OnCloseFindResults(object sender, RoutedEventArgs e)
        {
            this.lastResultsHeight = this.ResultsRow.Height;
            this.ResultsRow.Height = new GridLength(0);
            this.ResultsSplitter.Visibility = Visibility.Collapsed;
        }
    }
}