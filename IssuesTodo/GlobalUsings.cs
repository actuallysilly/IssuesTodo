// Resolve ambiguities introduced by UseWindowsForms + UseWPF coexisting.
// WinForms adds implicit globals for System.Drawing and System.Windows.Forms;
// these aliases ensure all existing code keeps resolving to the WPF types it already used.
global using Application     = System.Windows.Application;
global using Binding         = System.Windows.Data.Binding;
global using Button          = System.Windows.Controls.Button;
global using Color           = System.Windows.Media.Color;
global using ColorConverter  = System.Windows.Media.ColorConverter;
global using DragDropEffects = System.Windows.DragDropEffects;
global using DragEventArgs   = System.Windows.DragEventArgs;
global using KeyEventArgs    = System.Windows.Input.KeyEventArgs;
global using MessageBox      = System.Windows.MessageBox;
global using MouseEventArgs  = System.Windows.Input.MouseEventArgs;
global using OpenFileDialog  = Microsoft.Win32.OpenFileDialog;
global using Point           = System.Windows.Point;
global using Timer           = System.Threading.Timer;
global using UserControl     = System.Windows.Controls.UserControl;
