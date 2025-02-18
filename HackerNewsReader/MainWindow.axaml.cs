using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace HackerNewsReader
{
    public partial class MainWindow : Window
    {
        private HttpClient _httpClient = new HttpClient();
        private string _currentFeedType = "topstories";  // default feed
        private List<int> _topStoryIds = new List<int>();
        private int _currentIndex = 0;
        private const int BatchSize = 20;
        private bool _isLoadingMore = false;
        private ObservableCollection<HNItem> _feedItems = new ObservableCollection<HNItem>();
        private ListBox _feedListBox;
        private const string API_BASE = "https://hacker-news.firebaseio.com/v0";

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            LoadFeedView();
        }

        /// <summary>
        /// Creates the feed view – a ListBox wrapped in a ScrollViewer.
        /// </summary>
        private void LoadFeedView()
        {
            _feedItems.Clear();
            _topStoryIds.Clear();
            _currentIndex = 0;
            // Create the ListBox with a simple DataTemplate for each story.
            _feedListBox = new ListBox
            {
                ItemsSource = _feedItems,
                ItemTemplate = new FuncDataTemplate<HNItem>((item, _) =>
                {
                    var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(5) };
                    var titleText = new TextBlock
                    {
                        Text = item.title,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap
                    };
                    var metaText = new TextBlock
                    {
                        Text = $"by {item.by} | {UnixTimeToDateTime(item.time)} | {item.score} points | {(item.kids != null ? item.kids.Count : 0)} comments",
                        FontSize = 12,
                        Foreground = Brushes.Gray
                    };
                    panel.Children.Add(titleText);
                    panel.Children.Add(metaText);
                    return panel;
                }, true)
            };

            _feedListBox.SelectionChanged += FeedListBox_SelectionChanged;

            // Wrap the ListBox in a ScrollViewer and hook its scroll event for infinite scrolling.
            var scrollViewer = new ScrollViewer
            {
                Content = _feedListBox
            };
            scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            ContentArea.Content = scrollViewer;

            _ = LoadStoriesAsync();
        }

        /// <summary>
        /// Loads the list of story IDs from the current feed.
        /// </summary>
        private async Task LoadStoriesAsync()
        {
            try
            {
                var feedUrl = $"{API_BASE}/{_currentFeedType}.json";
                var response = await _httpClient.GetStringAsync(feedUrl);
                _topStoryIds = JsonSerializer.Deserialize<List<int>>(response);
                _currentIndex = 0;
                await LoadMoreStoriesAsync();
            }
            catch (Exception)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ContentArea.Content = new TextBlock { Text = "Error loading stories." };
                });
            }
        }

        /// <summary>
        /// Loads the next batch of stories.
        /// </summary>
        private async Task LoadMoreStoriesAsync()
        {
            if (_isLoadingMore)
                return;
            _isLoadingMore = true;
            int count = 0;
            while (_currentIndex < _topStoryIds.Count && count < BatchSize)
            {
                int storyId = _topStoryIds[_currentIndex++];
                _ = LoadStoryAsync(storyId);
                count++;
            }
            _isLoadingMore = false;
        }

        /// <summary>
        /// Loads a single story and adds it to the feed.
        /// </summary>
        private async Task LoadStoryAsync(int id)
        {
            try
            {
                var itemUrl = $"{API_BASE}/item/{id}.json";
                var response = await _httpClient.GetStringAsync(itemUrl);
                var story = JsonSerializer.Deserialize<HNItem>(response);
                if (story != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _feedItems.Add(story);
                    });
                }
            }
            catch { /* Ignore individual errors */ }
        }

        /// <summary>
        /// When the ScrollViewer nears the bottom, load more stories.
        /// </summary>
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                if (sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 100)
                {
                    _ = LoadMoreStoriesAsync();
                }
            }
        }

        /// <summary>
        /// When a story is selected from the feed, load its detail view.
        /// </summary>
        private void FeedListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_feedListBox.SelectedItem is HNItem selectedStory)
            {
                LoadDetailView(selectedStory);
            }
        }

        /// <summary>
        /// Loads the detail view for a story – shows its title, meta, optional URL and text, and then its comments.
        /// Comments are loaded asynchronously and appended as soon as each is available.
        /// </summary>
        private async void LoadDetailView(HNItem story)
        {
            // Clear selection
            _feedListBox.SelectedItem = null;
            var detailPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Back button to return to feed view.
            var backButton = new Button { Content = "← Back to Feed" };
            backButton.Click += (s, e) => { LoadFeedView(); };
            detailPanel.Children.Add(backButton);

            var titleText = new TextBlock
            {
                Text = story.title,
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap
            };
            detailPanel.Children.Add(titleText);

            var metaText = new TextBlock
            {
                Text = $"by {story.by} | {UnixTimeToDateTime(story.time)} | {story.score} points",
                FontSize = 12,
                Foreground = Brushes.Gray
            };
            detailPanel.Children.Add(metaText);

            if (!string.IsNullOrEmpty(story.url))
            {
                // For simplicity, we just show the text; adding hyperlink support is possible.
                var linkText = new TextBlock { Text = "Read more", Foreground = Brushes.Blue };
                detailPanel.Children.Add(linkText);
            }
            if (!string.IsNullOrEmpty(story.text))
            {
                var storyText = new TextBlock { Text = story.text, TextWrapping = TextWrapping.Wrap };
                detailPanel.Children.Add(storyText);
            }

            // Comments header.
            var commentsHeader = new TextBlock
            {
                Text = "Comments",
                FontSize = 16,
                FontWeight = FontWeight.Bold
            };
            detailPanel.Children.Add(commentsHeader);

            var commentsPanel = new StackPanel { Spacing = 5 };
            detailPanel.Children.Add(commentsPanel);

            if (story.kids != null && story.kids.Count > 0)
            {
                // For each top-level comment, load it asynchronously.
                foreach (var kidId in story.kids)
                {
                    _ = LoadCommentAsync(kidId, commentsPanel);
                }
            }
            else
            {
                commentsPanel.Children.Add(new TextBlock { Text = "No comments." });
            }

            // Wrap the detail view in a ScrollViewer.
            var detailScroll = new ScrollViewer { Content = detailPanel };
            ContentArea.Content = detailScroll;
        }

        /// <summary>
        /// Loads a comment (and its nested replies recursively) and appends it to the parent panel.
        /// </summary>
        private async Task LoadCommentAsync(int commentId, Panel parentPanel)
        {
            try
            {
                var itemUrl = $"{API_BASE}/item/{commentId}.json";
                var response = await _httpClient.GetStringAsync(itemUrl);
                var comment = JsonSerializer.Deserialize<HNItem>(response);
                if (comment != null && !comment.deleted && !comment.dead)
                {
                    var commentPanel = new StackPanel { Margin = new Thickness(20, 5, 0, 5) };
                    var meta = new TextBlock
                    {
                        Text = $"by {comment.by} | {UnixTimeToDateTime(comment.time)}",
                        FontSize = 12,
                        Foreground = Brushes.Gray
                    };
                    commentPanel.Children.Add(meta);
                    var text = new TextBlock
                    {
                        Text = comment.text,
                        TextWrapping = TextWrapping.Wrap
                    };
                    commentPanel.Children.Add(text);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        parentPanel.Children.Add(commentPanel);
                    });
                    // Load replies (nested comments) recursively.
                    if (comment.kids != null && comment.kids.Count > 0)
                    {
                        foreach (var kidId in comment.kids)
                        {
                            _ = LoadCommentAsync(kidId, commentPanel);
                        }
                    }
                }
            }
            catch { /* Ignore individual comment errors */ }
        }

        /// <summary>
        /// Converts Unix time (seconds) to a local DateTime.
        /// </summary>
        private DateTime UnixTimeToDateTime(long unixTime)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;
        }

        /// <summary>
        /// Handles the hamburger button click by opening a native menu for feed commands.
        /// </summary>
        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            // Create menu items
            var refreshItem = new MenuItem { Header = "Refresh" };
            refreshItem.Click += (s, args) => { LoadFeedView(); };

            var topStoriesItem = new MenuItem { Header = "Top Stories" };
            topStoriesItem.Click += (s, args) =>
            {
                _currentFeedType = "topstories";
                LoadFeedView();
            };

            var newStoriesItem = new MenuItem { Header = "New Stories" };
            newStoriesItem.Click += (s, args) =>
            {
                _currentFeedType = "newstories";
                LoadFeedView();
            };

            var bestStoriesItem = new MenuItem { Header = "Best Stories" };
            bestStoriesItem.Click += (s, args) =>
            {
                _currentFeedType = "beststories";
                LoadFeedView();
            };

            var askHnItem = new MenuItem { Header = "Ask HN" };
            askHnItem.Click += (s, args) =>
            {
                _currentFeedType = "askstories";
                LoadFeedView();
            };

            var showHnItem = new MenuItem { Header = "Show HN" };
            showHnItem.Click += (s, args) =>
            {
                _currentFeedType = "showstories";
                LoadFeedView();
            };

            var jobStoriesItem = new MenuItem { Header = "Job Stories" };
            jobStoriesItem.Click += (s, args) =>
            {
                _currentFeedType = "jobstories";
                LoadFeedView();
            };

            // Create the context menu and add a separator between Refresh and the feed items.
            var contextMenu = new ContextMenu
            {
                ItemsSource = new List<object>
                {
                    refreshItem,
                    new Separator(),
                    topStoriesItem,
                    newStoriesItem,
                    bestStoriesItem,
                    askHnItem,
                    showHnItem,
                    jobStoriesItem
                }
            };

            // Set the placement target and open the context menu.
            if (sender is Control btn)
            {
                contextMenu.PlacementTarget = btn;
                // Pass the button as a parameter to Open() so it knows what to anchor to.
                contextMenu.Open(btn);
            }
        }
    }
}
