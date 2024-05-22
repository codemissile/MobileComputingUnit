using Microsoft.Maui.Devices.Sensors;

namespace MauiApp2;

public partial class Sensors : ContentPage
{
    private bool isUpdatingLocation = false;
    public Sensors()
    {
        InitializeComponent();
    }

    private void OnToggleAccelerometerToggled(object sender, ToggledEventArgs e)
    {
        ToggleAccelerometer(e.Value);
    }

    private void OnToggleGyroscopeToggled(object sender, ToggledEventArgs e)
    {
        ToggleGyroscope(e.Value);
    }

    private void OnTogglePositionToggled(object sender, ToggledEventArgs e)
    {
        if (e.Value)
        {
            StartLocationUpdates();
        }
        else
        {
            StopLocationUpdates();
        }
    }

    private async void StartLocationUpdates()
    {
        isUpdatingLocation = true;
        while (isUpdatingLocation)
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
                var location = await Geolocation.GetLocationAsync(request);

                if (location != null)
                {
                    UpdateLocationUI(location);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to get location: {ex.Message}");
            }
            await Task.Delay(10000); // Update every 10 seconds, adjust as necessary
        }
    }

    private void StopLocationUpdates()
    {
        isUpdatingLocation = false;
    }

    private void UpdateLocationUI(Location location)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            latitudeResult.Text = $"Latitude: {location.Latitude}";
            longitudeResult.Text = $"Longitude: {location.Longitude}";
            altitudeResult.Text = $"Altitude: {location.Altitude ?? 0} m";
            speedResult.Text = $"Speed: {location.Speed ?? 0} km/h";
        });
    }

    private void ToggleAccelerometer(bool shouldStart)
    {
        if (Accelerometer.Default.IsSupported)
        {
            if (shouldStart && !Accelerometer.Default.IsMonitoring)
            {
                Accelerometer.ReadingChanged += Accelerometer_ReadingChanged;
                Accelerometer.Start(SensorSpeed.UI);
            }
            else if (!shouldStart && Accelerometer.Default.IsMonitoring)
            {
                Accelerometer.Stop();
                Accelerometer.ReadingChanged -= Accelerometer_ReadingChanged;
            }
        }
    }

    private void ToggleGyroscope(bool shouldStart)
    {
        if (Gyroscope.Default.IsSupported)
        {
            if (shouldStart && !Gyroscope.Default.IsMonitoring)
            {
                Gyroscope.ReadingChanged += Gyroscope_ReadingChanged;
                Gyroscope.Start(SensorSpeed.UI);
            }
            else if (!shouldStart && Gyroscope.Default.IsMonitoring)
            {
                Gyroscope.Stop();
                Gyroscope.ReadingChanged -= Gyroscope_ReadingChanged;
            }
        }
    }


    private void Accelerometer_ReadingChanged(object? sender, AccelerometerChangedEventArgs e)
    {
        var data = e.Reading;
        double magnitude = Math.Sqrt(Math.Pow(data.Acceleration.X, 2) + Math.Pow(data.Acceleration.Y, 2) + Math.Pow(data.Acceleration.Z, 2));

        MainThread.BeginInvokeOnMainThread(() =>
        {
            xResult.Text = $"X: {data.Acceleration.X}";
            yResult.Text = $"Y: {data.Acceleration.Y}";
            zResult.Text = $"Z: {data.Acceleration.Z}";
            AccelLabel.Text = $"Magnitude: {magnitude:N2}";
        });
    }

    private void Gyroscope_ReadingChanged(object? sender, GyroscopeChangedEventArgs e)
    {
        var data = e.Reading;

        // Calculate the magnitude of the angular velocity vector
        double magnitude = Math.Sqrt(Math.Pow(data.AngularVelocity.X, 2) +
                                     Math.Pow(data.AngularVelocity.Y, 2) +
                                     Math.Pow(data.AngularVelocity.Z, 2));

        MainThread.BeginInvokeOnMainThread(() => {
            xGyroResult.Text = $"X: {data.AngularVelocity.X:N3}";
            yGyroResult.Text = $"Y: {data.AngularVelocity.Y:N3}";
            zGyroResult.Text = $"Z: {data.AngularVelocity.Z:N3}";
            gyroMagnitudeResult.Text = $"Magnitude: {magnitude:N2}";
        });
    }

}