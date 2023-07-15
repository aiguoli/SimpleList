using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList.Controls
{
    public sealed partial class SettingsCard : UserControl
    {
        public SettingsCard()
        {
            InitializeComponent();
        }
        
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));
        
        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }
        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register("Description", typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

        public IconElement Icon
        {
            get => (IconElement)GetValue(IconProperty);
            set =>SetValue(IconProperty, value);
        }
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register("Icon", typeof(string), typeof(SettingsCard), new PropertyMetadata(null));

        public static new readonly DependencyProperty ContentProperty =
    DependencyProperty.Register(nameof(Content), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

        public new object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }
    }
}
