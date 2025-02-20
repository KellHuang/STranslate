﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using STranslate.Helper;
using STranslate.Log;
using STranslate.Model;
using STranslate.Util;
using STranslate.ViewModels.Preference.Translator;
using STranslate.Views.Preference.Translator;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace STranslate.ViewModels.Preference.OCR;

public abstract partial class OCRLLMBase : OCRBase
{
    [JsonIgnore][ObservableProperty] private double _temperature = 1.0;

    [JsonIgnore]
    [ObservableProperty]
    [property: DefaultValue("")]
    [property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    private string _model = "gpt-4o-2024-08-06";

    public abstract BindingList<UserDefinePrompt> UserDefinePrompts { get; set; }

    [RelayCommand]
    [property: JsonIgnore]
    private void SelectedPrompt(List<object> obj)
    {
        var userDefinePrompt = (UserDefinePrompt)obj.First();
        foreach (var item in UserDefinePrompts) item.Enabled = false;
        userDefinePrompt.Enabled = true;
        ManualPropChanged(nameof(UserDefinePrompts));

        if (obj.Count == 2)
            Singleton<TranslatorViewModel>.Instance.SaveCommand.Execute(null);
    }

    [RelayCommand]
    [property: JsonIgnore]
    private void UpdatePrompt(UserDefinePrompt userDefinePrompt)
    {
        var dialog = new PromptDialog(ServiceType.OpenAIService, (UserDefinePrompt)userDefinePrompt.Clone());
        if (!(dialog.ShowDialog() ?? false)) return;
        var tmp = ((PromptViewModel)dialog.DataContext).UserDefinePrompt;
        userDefinePrompt.Name = tmp.Name;
        userDefinePrompt.Prompts = tmp.Prompts;
        ManualPropChanged(nameof(UserDefinePrompts));
    }

    [RelayCommand]
    [property: JsonIgnore]
    private void DeletePrompt(UserDefinePrompt userDefinePrompt)
    {
        UserDefinePrompts.Remove(userDefinePrompt);
        ManualPropChanged(nameof(UserDefinePrompts));
    }

    [RelayCommand]
    [property: JsonIgnore]
    private void AddPrompt()
    {
        var userDefinePrompt = new UserDefinePrompt("Undefined", []);
        var dialog = new PromptDialog(ServiceType.OpenAIService, userDefinePrompt);
        if (!(dialog.ShowDialog() ?? false)) return;
        var tmp = ((PromptViewModel)dialog.DataContext).UserDefinePrompt;
        userDefinePrompt.Name = tmp.Name;
        userDefinePrompt.Prompts = tmp.Prompts;
        UserDefinePrompts.Add(userDefinePrompt);
        ManualPropChanged(nameof(UserDefinePrompts));
    }

    [RelayCommand]
    [property: JsonIgnore]
    private void AddPromptFromDrop(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        // 取第一个文件
        var filePath = files[0];

        if (Path.GetExtension(filePath).Equals(".json", StringComparison.CurrentCultureIgnoreCase))
        {
            PromptFileHandle(filePath);
            ToastHelper.Show("导入成功", WindowType.Preference);
        }
        else
            ToastHelper.Show("请拖入Prompt文件", WindowType.Preference);
    }

    [RelayCommand]
    [property: JsonIgnore]
    private void AddPromptFromFile()
    {
        var openFileDialog = new OpenFileDialog { Filter = "json(*.json)|*.json" };
        if (openFileDialog.ShowDialog() != true)
            return;
        PromptFileHandle(openFileDialog.FileName);
    }

    private void PromptFileHandle(string path)
    {
        var jsonStr = File.ReadAllText(path);
        try
        {
            var prompt = JsonConvert.DeserializeObject<UserDefinePrompt>(jsonStr);
            if (prompt is { Name: not null, Prompts: not null })
            {
                prompt.Enabled = false;
                UserDefinePrompts.Add(prompt);
                ManualPropChanged(nameof(UserDefinePrompts));
            }
            else
            {
                ToastHelper.Show("导入内容为空", WindowType.Preference);
            }
        }
        catch
        {
            try
            {
                var prompt = JsonConvert.DeserializeObject<List<UserDefinePrompt>>(jsonStr);
                if (prompt != null)
                {
                    foreach (var item in prompt)
                    {
                        item.Enabled = false;
                        UserDefinePrompts.Add(item);
                    }
                    ManualPropChanged(nameof(UserDefinePrompts));
                }
                else
                {
                    ToastHelper.Show("导入内容为空", WindowType.Preference);
                }
            }
            catch (Exception e)
            {
                LogService.Logger.Error($"导入Prompt失败: {e.Message}", e);
                ToastHelper.Show("导入失败", WindowType.Preference);
            }
        }
    }

    [RelayCommand]
    [property: JsonIgnore]
    private void Export()
    {
        string jsonStr;
        StringBuilder sb = new($"{Name}_Prompt_");
        if ((Keyboard.Modifiers & ModifierKeys.Control) <= 0)
        {
            var selectedPrompt = UserDefinePrompts.FirstOrDefault(x => x.Enabled);
            if (selectedPrompt == null)
            {
                ToastHelper.Show("未选择Prompt", WindowType.Preference);
                return;
            }
            jsonStr = JsonConvert.SerializeObject(selectedPrompt, Formatting.Indented);
            sb.Append(selectedPrompt.Name);
        }
        else
        {
            jsonStr = JsonConvert.SerializeObject(UserDefinePrompts, Formatting.Indented);
            sb.Append("All");
        }
        sb.Append($"_{DateTime.Now:yyyyMMddHHmmss}");
        var saveFileDialog = new SaveFileDialog { Filter = "json(*.json)|*.json", FileName = sb.ToString() };

        if (saveFileDialog.ShowDialog() != true) return;
        File.WriteAllText(saveFileDialog.FileName, jsonStr);
        ToastHelper.Show("导出成功", WindowType.Preference);
    }

    public void ManualPropChanged(params string[] array)
    {
        foreach (var str in array) OnPropertyChanged(str);
    }
}
