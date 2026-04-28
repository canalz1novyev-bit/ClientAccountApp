using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ClientAccountApp
{
    public sealed partial class ProblemSignaturesPage : Page
    {
        private readonly ObservableCollection<ProblemSignatureItem> _problemSignatures = new();
        private ProblemSignatureItem? _selectedProblemSignature;

        public ProblemSignaturesPage()
        {
            this.InitializeComponent();

            ProblemSignaturesListView.ItemsSource = _problemSignatures;
            ProblemSignatureFilterComboBox.SelectedIndex = 0;

            Loaded += ProblemSignaturesPage_Loaded;
        }

        private void ProblemSignaturesPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProblemSignatures();
        }

        private void LoadProblemSignatures()
        {
            _problemSignatures.Clear();
            _selectedProblemSignature = null;
            ClearProblemSignatureDetails();

            using (var db = new AppDbContext())
            {
                var items = db.DigitalSignatures
                    .AsNoTracking()
                    .Include(s => s.ClientInfo)
                    .ToList()
                    .Where(s => s.ClientInfo != null)
                    .Select(s =>
                    {
                        int daysLeft = (s.ExpiresDate.Date - DateTime.Today).Days;

                        return new ProblemSignatureItem
                        {
                            ClientId = s.ClientInfoId,
                            SignatureId = s.Id,
                            ClientName = s.ClientInfo!.Name,
                            Inn = s.ClientInfo!.Inn,
                            CertificationAuthority = s.CertificationAuthority,
                            Comment = s.Comment,
                            ExpiresDate = s.ExpiresDate,
                            DaysLeft = daysLeft
                        };
                    })
                    .Where(MatchesProblemSignatureFilter)
                    .OrderBy(i => i.DaysLeft)
                    .ThenBy(i => i.ClientName)
                    .ToList();

                foreach (var item in items)
                {
                    _problemSignatures.Add(item);
                }
            }
        }

        private bool MatchesProblemSignatureFilter(ProblemSignatureItem item)
        {
            if (ProblemSignatureFilterComboBox == null)
            {
                return item.DaysLeft <= 30;
            }

            int filterIndex = ProblemSignatureFilterComboBox.SelectedIndex;

            return filterIndex switch
            {
                0 => item.DaysLeft <= 30,
                1 => item.DaysLeft >= 0 && item.DaysLeft <= 30,
                2 => item.DaysLeft >= 0 && item.DaysLeft <= 7,
                3 => item.DaysLeft < 0,
                _ => item.DaysLeft <= 30
            };
        }

        private void ProblemSignatureFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProblemSignatureFilterComboBox == null || ProblemSignaturesListView == null)
            {
                return;
            }

            LoadProblemSignatures();
        }

        private void RefreshProblemSignaturesButton_Click(object sender, RoutedEventArgs e)
        {
            LoadProblemSignatures();
        }

        private void ProblemSignaturesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProblemSignaturesListView.SelectedItem is ProblemSignatureItem item)
            {
                _selectedProblemSignature = item;
                ShowProblemSignatureDetails(item);
            }
            else
            {
                _selectedProblemSignature = null;
                ClearProblemSignatureDetails();
            }
        }

        private void ShowProblemSignatureDetails(ProblemSignatureItem item)
        {
            ProblemClientNameTextBlock.Text = item.ClientName;
            ProblemInnTextBlock.Text = item.Inn;
            ProblemAuthorityTextBlock.Text = item.CertificationAuthority;
            ProblemExpiresTextBlock.Text = item.ExpiresDateText;
            ProblemStatusTextBlock.Text = item.StatusText;
            ProblemCommentTextBlock.Text = string.IsNullOrWhiteSpace(item.Comment) ? "—" : item.Comment;
        }

        private void ClearProblemSignatureDetails()
        {
            ProblemClientNameTextBlock.Text = "—";
            ProblemInnTextBlock.Text = "—";
            ProblemAuthorityTextBlock.Text = "—";
            ProblemExpiresTextBlock.Text = "—";
            ProblemStatusTextBlock.Text = "—";
            ProblemCommentTextBlock.Text = "—";
        }

        private void OpenProblemClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProblemSignature == null)
            {
                return;
            }

            Frame?.Navigate(typeof(LegacyWorkspacePage), _selectedProblemSignature.ClientId);
        }
    }
}