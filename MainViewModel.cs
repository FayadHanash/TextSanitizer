using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextSanitizer;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    string? text;
    [ObservableProperty]
    FormattedString? outText;
    [ObservableProperty]
    string? author;

    public MainViewModel()
    {
        Author = $" \u00A9 {DateTime.Now.Year} by Fayad";
    }

    [RelayCommand]
    async void Clean()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Text)) return;
            OutText = SanitizeText.Sanitize(Text);


            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            OutText = ex.Message.ToString();
        }
    }

    [RelayCommand]
    async void ShowHidden()
    {
        try
        {
            OutText = SanitizeText.ShowHiddenChars(Text);

            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            OutText = ex.Message.ToString();
        }
    }



    [RelayCommand]
    async void Copy()
    {
        if (string.IsNullOrWhiteSpace(OutText?.ToString())) return;
        await Clipboard.Default.SetTextAsync(OutText?.ToString());
    }

    [RelayCommand]
    async void Empty()
    {
        Text = string.Empty;
        OutText = string.Empty;
    }



}