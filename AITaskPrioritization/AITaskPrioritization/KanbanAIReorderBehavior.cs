using System.Text.Json;
using Syncfusion.Maui.Buttons;
using Syncfusion.Maui.Kanban;

namespace AITaskPrioritization
{
    public class KanbanAIReorderBehavior : Behavior<ContentPage>
    {
        private SfButton? button;
        private Label? infoLabel;
        private SfKanban? kanban;
        private ViewModel? viewModel;
        private AzureOpenAIBaseService aiService = new AzureOpenAIBaseService();

        protected override void OnAttachedTo(ContentPage bindable)
        {
            base.OnAttachedTo(bindable);

            bindable.BindingContextChanged += (s, e) =>
            {
                viewModel = bindable.BindingContext as ViewModel;
            };

            button = bindable.FindByName<SfButton>("aiSortButton");
            infoLabel = bindable.FindByName<Label>("infoLabel");
            kanban = bindable.FindByName<SfKanban>("kanbanBoard");

            if (button != null)
            {
                button.Clicked += OnAISortClicked;
            }
        }

        private async void OnAISortClicked(object? sender, EventArgs e)
        {
            if (viewModel == null) return;

            button!.IsEnabled = false;
            infoLabel!.IsVisible = false;
            await Task.Delay(50);
            infoLabel.Text = "AI analyzing tasks...";
            infoLabel.IsVisible = true;
            infoLabel.Opacity = 0;
            await infoLabel.FadeToAsync(1, 150);
            await Task.Delay(50);

            if (!aiService.IsCredentialValid)
            {
                await StopUI("Invalid AI credentials");
                return;
            }

            if (kanban != null)
            {
                await kanban.FadeToAsync(0.3, 150);
            }

            var activeTasks = viewModel.Cards.Where(c => c.Category != "Done").ToList();
            var doneTasks = viewModel.Cards.Where(c => c.Category == "Done").OrderBy(c => c.Index).ToList();
            var grouped = activeTasks.GroupBy(c => c.Category).ToList();
            var finalList = new List<CardDetails>();

            foreach (var group in grouped)
            {
                var tasks = group.ToList();
                string prompt = BuildPrompt(tasks);
                string result = await aiService.GetAIResponse(prompt);
                string cleaned = result.Replace("```json", "").Replace("```", "").Replace("\n", "").Replace("\r", "").Trim();
                var sorted = ApplyAIResult(tasks, cleaned);
                finalList.AddRange(sorted);
            }

            finalList.AddRange(doneTasks);
            viewModel.Cards.Clear();
            int index = 1;

            foreach (var card in finalList)
            {
                card.Index = index++;
                viewModel.Cards.Add(card);
            }

            var topTasks = viewModel.Cards.Where(c => c.Category != "Done").GroupBy(c => c.Category).Select(g => g.First()).ToList();

            foreach (var task in topTasks)
            {
                _ = ApplyGlowEffect(task); 
            }

            if (kanban != null)
            {
                kanban.TranslationY = 30;

                await Task.WhenAll(
                    kanban.FadeToAsync(1, 300),
                    kanban.TranslateToAsync(0, 0, 300, Easing.CubicIn)
                );
            }

            await StopUI("✅ AI prioritization completed");
        }

        private string BuildPrompt(List<CardDetails> tasks)
        {
            string taskText = string.Join("\n",
                tasks.Select(t =>
                    $"Title: {t.Title}, Description: {t.Description}, DueDate: {t.DueDate:yyyy-MM-dd}")
            );

            return $@"
                    You are an AI task prioritization engine.
                    Analyze the following tasks and determine priority.
                    RULES:
                    1. Tasks with nearer DueDate = higher priority
                    2. If tasks have SAME DueDate:
                       - prioritize based on IMPACT (critical, backend, integrations)
                       - prioritize tasks affecting other tasks (DEPENDENCIES)
                    3. Payment, API, security, core system = HIGH priority
                    4. UI, cosmetic changes = LOW priority
                    Return ONLY JSON array of Titles in correct order.
                    Tasks:{taskText}";
        }

        private List<CardDetails> ApplyAIResult(List<CardDetails> tasks, string aiResult)
        {
            try
            {
                int start = aiResult.IndexOf("[");
                int end = aiResult.LastIndexOf("]");

                if (start == -1 || end == -1)
                {
                    throw new Exception();
                }

                string json = aiResult.Substring(start, end - start + 1);
                var titles = JsonSerializer.Deserialize<List<string>>(json);
                return tasks.OrderBy(t => titles?.IndexOf(t.Title!) ?? int.MaxValue).ToList();
            }
            catch
            {
                return tasks.OrderBy(t => t.DueDate).ToList();
            }
        }


        private async Task StopUI(string message)
        {
            if (infoLabel != null)
            {
                infoLabel.Text = message;
                infoLabel.IsVisible = true;
                infoLabel.Opacity = 1;
                await Task.Delay(2000);
                await infoLabel.FadeToAsync(0, 300);
                infoLabel.IsVisible = false;
            }

            if (button != null)
            {
                button.IsEnabled = true;
            }
        }


        private async Task ApplyGlowEffect(CardDetails card)
        {
            try
            {
                card.UrgencyStrokeColor = Colors.PapayaWhip;
                card.StrokeThickness = 3;

                for (int i = 0; i < 3; i++)
                {
                    card.GlowOpacity = 0.6;
                    await Task.Delay(400);
                    card.GlowOpacity = 1;
                    await Task.Delay(200);
                }

                await Task.Delay(1200);
                card.StrokeThickness = 0;
                card.UrgencyStrokeColor = Colors.Gold;
                card.GlowOpacity = 1;
            }
            catch
            {
                
            }
        }

        protected override void OnDetachingFrom(ContentPage bindable)
        {
            if (button != null)
            {
                button.Clicked -= OnAISortClicked;
            }

            base.OnDetachingFrom(bindable);
        }
    }
}
