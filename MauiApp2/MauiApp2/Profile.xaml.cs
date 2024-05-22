using MauiApp2;
using Microsoft.Maui.Controls;
using System;

namespace MauiApp2
{
    public partial class Profile : ContentPage
    {
        private viewDataModel viewModel;
        public Profile()
        {
            InitializeComponent();
            viewModel = new viewDataModel();
            BindingContext = viewModel;
        }

        private async void NavigateToMainPageButton_Clicked(object sender, EventArgs e)
        {
            // Create an instance of viewDataModel
            viewDataModel viewModel = new viewDataModel();

            // Navigate to MainPage and pass the viewDataModel instance to its constructor
            MainPage mainPage = new MainPage(viewModel);
            await Navigation.PushAsync(mainPage);
        }



        private void OnEntryTextChanged(object sender, TextChangedEventArgs e)
        {
            // Handle TextChanged event for all Entry controls
            Entry entry = (Entry)sender;
        }

        private void OnEntryCompleted(object sender, EventArgs e)
        {
            // Handle Completed event for all Entry controls
            Entry entry = (Entry)sender;
            string text = entry.Text;
        }

        private async void ConfirmButton_Clicked(object sender, EventArgs e)
        {
            string error;
            if (!ValidateName(FirstNameEntry.Text, out error))
            {
                await DisplayAlert("Validation Error", error, "OK");
                return;
            }
            if (!ValidateName(LastNameEntry.Text, out error))
            {
                await DisplayAlert("Validation Error", error, "OK");
                return;
            }
            if (!ValidatePhoneNumber(PhoneNumberEntry.Text, out error))
            {
                await DisplayAlert("Validation Error", error, "OK");
                return;
            }
            if (!ValidateEmail(EmailEntry.Text, out error))
            {
                await DisplayAlert("Validation Error", error, "OK");
                return;
            }

            viewModel.SurName = LastNameEntry.Text;
            viewModel.PhoneNumber = PhoneNumberEntry.Text;
            viewModel.Email = EmailEntry.Text;

            // Now that data is validated, process it.
            string message = $"Name: {FirstNameEntry.Text} {LastNameEntry.Text}\nPhone: {PhoneNumberEntry.Text}\nEmail: {EmailEntry.Text}";
            await DisplayAlert("Profile Information", message, "OK");

            // Navigate to MainPage
            await Navigation.PushAsync(new MainPage(viewModel));
        }


        private bool ValidateName(string name, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                errorMessage = "Name cannot be empty.";
                return false;
            }
            errorMessage = "";
            return true;
        }

        private bool ValidatePhoneNumber(string phoneNumber, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                errorMessage = "Phone number cannot be empty.";
                return false;
            }
            if (!phoneNumber.All(char.IsDigit))
            {
                errorMessage = "Phone number can only contain digits.";
                return false;
            }
            errorMessage = "";
            return true;
        }

        private bool ValidateEmail(string email, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                errorMessage = "Email cannot be empty.";
                return false;
            }
            if (!email.Contains('@') || !email.Contains('.'))
            {
                errorMessage = "Enter a valid email address.";
                return false;
            }
            errorMessage = "";
            return true;
        }

    }
}
