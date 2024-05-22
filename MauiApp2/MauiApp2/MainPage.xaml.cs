using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Maui.Controls;
using Microsoft.ML.Transforms.Onnx;
using MauiApp2;
using System.IO;




namespace MauiApp2
{
    // Main application page Class
    public partial class MainPage : ContentPage
    {
        //Timer and control variable
        private Stopwatch stopwatch;
        private bool isRunning = false;
        private bool isLongPress = false;
        private DateTime pressTime;
        private System.Threading.Timer? uiTimer;
        private TimeOnly time = new();

        //senosr data management 
        private bool sensorsInitialized = false;
        private Stopwatch sensorUpdateStopwatch = new Stopwatch();
        private const int SensorUpdateInterval = 100; // milliseconds for 10 Hz
        private double lastAccelMagnitude = 0;
        private double lastGyroMagnitude = 0;
        private List<(DateTime timestamp, double accelMag, double gyroMag)> sensorData = new();
        private const int CaptureWindow = 15000; // Total ms to capture data
        private List<SensorData> sensorDataList = new List<SensorData>();

        //viewModel for data binding 
        private viewDataModel viewModel;

        // Dictionary to hold probability scores
        public IDictionary<long, float> Probability { get; set; } = new Dictionary<long, float>();

        // Path to the ONNX model
        private static readonly string ONNX_MODEL_PATH = "random_forest_model.onnx";

        private InferenceSession ?session;
        private PredictionEngine<ModelInput, ModelOutput>? predictionEngine;
        private MLContext mlContext;

        private bool isUpdatingLocation = false;

        // Data class for storing sensor readings
        private class SensorData
        {
            public DateTime Timestamp { get; set; }
            public double AccelMag { get; set; }
            public double GyroMag { get; set; }
            public double LastAccelMag { get; set; }
            public double LastGyroMag { get; set; }
            // Change rates
            public double SmoothedAccelMag { get; set; }
            public double SmoothedGyroMag { get; set; }
            public double AccelChange => AccelMag - LastAccelMag;
            public double GyroChange => GyroMag - LastGyroMag;
        }

        // Model input class
        public class ModelInput
        {
            [ColumnName("float_input")]
            public float[] Features { get; set; } = new float[5]; // Initialize with default size

        }

        // Model output class
        public class ModelOutput
        {
            [ColumnName("output_label")]
            public long PredictedLabel { get; set; }
            [ColumnName("output_probability")]
            public float[] Probabilities { get; set; } = new float[2]; // Initialize with default size

        }

        //contructor initialises sensors and UI componenets
        public MainPage(viewDataModel viewModel)
        {
            InitializeComponent();
            stopwatch = new Stopwatch();
            this.viewModel = viewModel;
            BindingContext = this.viewModel;

            

            // Initialize the ML model
            InitializeModel();

            // Display the phone number in a label or wherever you want
            DisplayPhoneNumber();


        }

        // Button click event handler
        private void OnTestModelButtonClicked(object sender, EventArgs e)
        {
            TestModelWithSampleInput();
        }

        // Initialize the ONNX model
        private void InitializeModel()
        {
            try
            {
                // Verify that the file exists
                if (!System.IO.File.Exists(ONNX_MODEL_PATH))
                {
                    throw new FileNotFoundException("ONNX model file not found.", ONNX_MODEL_PATH);
                }

                mlContext = new MLContext();
                var onnxEstimator = mlContext.Transforms.ApplyOnnxModel(modelFile: ONNX_MODEL_PATH);
                var emptyData = mlContext.Data.LoadFromEnumerable(new List<ModelInput>());
                var onnxModel = onnxEstimator.Fit(emptyData);
                predictionEngine = mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(onnxModel);

                Debug.WriteLine("Model loaded successfully.");
            }
            catch (FileNotFoundException ex)
            {
                Debug.WriteLine($"File not found: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DisplayAlert("Error", $"Failed to load the ONNX model. File not found: {ex.Message}", "OK");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing the ONNX model: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DisplayAlert("Error", $"Failed to load the ONNX model. Please check the model path.\n{ex.Message}", "OK");
                });
            }
        }





        // Initialize accelerometer and gyroscope sensors safely
        private void InitializeSensors()
        {
            // Check if sensors have already been initialized to prevent multiple bindings and starts
            if (!sensorsInitialized)
            {
                // Start accelerometer if it's not already monitoring
                if (!Accelerometer.IsMonitoring)
                {
                    Accelerometer.ReadingChanged += OnAccelerometerReadingChanged;
                    Accelerometer.Start(SensorSpeed.UI);  // Choose appropriate SensorSpeed
                }

                // Start gyroscope if it's not already monitoring
                if (!Gyroscope.IsMonitoring)
                {
                    Gyroscope.ReadingChanged += OnGyroscopeReadingChanged;
                    Gyroscope.Start(SensorSpeed.UI);  // Choose appropriate SensorSpeed
                }

                StartLocationUpdates();
                // Set flag to true indicating sensors are initialized
                sensorsInitialized = true;

                // Start or reset the stopwatch for timing sensor data updates
                sensorUpdateStopwatch.Start();

                // Clear existing sensor data to start fresh
                sensorDataList.Clear();
            }
        }

        /// Method to stop and clean up sensors safely
        private void CleanupSensors()
        {
            // Ensure sensors are stopped and event handlers are detached
            if (sensorsInitialized)
            {
                if (Accelerometer.IsMonitoring)
                {
                    Accelerometer.Stop();
                    Accelerometer.ReadingChanged -= OnAccelerometerReadingChanged;
                }

                if (Gyroscope.IsMonitoring)
                {
                    Gyroscope.Stop();
                    Gyroscope.ReadingChanged -= OnGyroscopeReadingChanged;
                }

                StopLocationUpdates();
                // Reset initialization flag
                sensorsInitialized = false;

                // Stop the stopwatch associated with sensor updates
                sensorUpdateStopwatch.Stop();
            }
        }

        // Stop all sensor monitoring and reset UI
        private void StopActivity()
        {
            if (isRunning)
            {
                stopwatch.Stop();
                isRunning = false;
                startStopButton.Text = "Start";
                CleanupSensors();
                uiTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                uiTimer?.Dispose();
                uiTimer = null;
                stopwatchTime.Text = $"Time Finished: {stopwatch.Elapsed}";
                stopwatch.Reset();
                time = time.Add(TimeSpan.FromMilliseconds(1));
                SetTime();

                lastAccelMagnitude = 0;  // Reset magnitudes
                lastGyroMagnitude = 0;
            }
        }



        private void OnAccelerometerReadingChanged(object sender, AccelerometerChangedEventArgs e)
        {
            UpdateSensorData(e.Reading.Acceleration.X, e.Reading.Acceleration.Y, e.Reading.Acceleration.Z, true);

            // Update the labels with the latest accelerometer data
            var data = e.Reading;
            double magnitude = Math.Sqrt(Math.Pow(data.Acceleration.X, 2) + Math.Pow(data.Acceleration.Y, 2) + Math.Pow(data.Acceleration.Z, 2));           
            lastAccelMagnitude = magnitude;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                xResult.Text = $"AcclX: {data.Acceleration.X}";
                yResult.Text = $"AccelY: {data.Acceleration.Y}";
                zResult.Text = $"AccelZ: {data.Acceleration.Z}";
                AccelLabel.Text = $"AccelMagnitude: {magnitude:N2}";
            });
        }

        private void OnGyroscopeReadingChanged(object? sender, GyroscopeChangedEventArgs e)
        {
            UpdateSensorData(e.Reading.AngularVelocity.X, e.Reading.AngularVelocity.Y, e.Reading.AngularVelocity.Z, false);

            // Update the labels with the latest gyroscope data
            var data = e.Reading;
            double magnitude = Math.Sqrt(Math.Pow(data.AngularVelocity.X, 2) + Math.Pow(data.AngularVelocity.Y, 2) + Math.Pow(data.AngularVelocity.Z, 2));
            lastGyroMagnitude = magnitude;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                xGyroResult.Text = $"GyroX: {data.AngularVelocity.X:N3}";
                yGyroResult.Text = $"GyroY: {data.AngularVelocity.Y:N3}";
                zGyroResult.Text = $"GyroZ: {data.AngularVelocity.Z:N3}";
                gyroMagnitudeResult.Text = $"GyroMagnitude: {magnitude:N2}";
            });
        }

        // Start listening for location changes
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
                await Task.Delay(100); // Update every 100 miliseconds, adjust as necessary
            }
        }

        private void StopLocationUpdates()
        {
            isUpdatingLocation = false;
        }

        private void UpdateLocationUI(Location location)
        {
            MainThread.BeginInvokeOnMainThread(() => {
                speedResult.Text = $"Speed: {location.Speed ?? 0} km/h";
            });
        }

        // Update sensor data based on sensor type (accelerometer or gyroscope)
        private void UpdateSensorData(double x, double y, double z, bool isAccel)
        {
            if (sensorUpdateStopwatch.ElapsedMilliseconds < SensorUpdateInterval) return;
            sensorUpdateStopwatch.Restart();

            var magnitude = Math.Sqrt(x * x + y * y + z * z);
            var now = DateTime.UtcNow;
            var lastData = sensorDataList.LastOrDefault();
            var newData = new SensorData
            {
                Timestamp = now,
                AccelMag = isAccel ? magnitude : lastData?.AccelMag ?? 0,
                GyroMag = !isAccel ? magnitude : lastData?.GyroMag ?? 0,
                LastAccelMag = lastData?.AccelMag ?? 0,
                LastGyroMag = lastData?.GyroMag ?? 0,
            };

            sensorDataList.Add(newData);
            CalculateRollingMean(sensorDataList, 5);
            CheckForPotentialCrash();
            
            sensorUpdateStopwatch.Restart(); // Reset the stopwatch after updating
        }
        // Calculate the rolling mean for smoothing sensor data
        private void CalculateRollingMean(List<SensorData> data, int windowSize)
        {
            for (int i = 0; i < data.Count; i++)
            {
                double sumAccel = 0;
                double sumGyro = 0;
                int count = 0;

                // Sum up to 'windowSize' elements preceding and including the current element
                for (int j = Math.Max(0, i - windowSize + 1); j <= i; j++)
                {
                    sumAccel += data[j].AccelMag;
                    sumGyro += data[j].GyroMag;
                    count++;
                }

                // Calculate the mean and assign it to the smoothed properties
                data[i].SmoothedAccelMag = sumAccel / count;
                data[i].SmoothedGyroMag = sumGyro / count;
            }
        }

        // Method called to check for potential crashes
        private void CheckForPotentialCrash()
        {
            // Ensure there is enough data to analyze
            if (sensorDataList.Count >= 2)
            {
                var currentData = sensorDataList.Last();
                var previousData = sensorDataList[^2];

                // Calculate the change in accelerometer and gyroscope magnitudes
                double accelChange = Math.Abs(currentData.AccelMag - previousData.AccelMag);
                double gyroChange = Math.Abs(currentData.GyroMag - previousData.GyroMag);

                // Check if the change exceeds the threshold
                if (accelChange > 1.0 && gyroChange > 1.0)
                {
                    // Show alert for potential crash
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Alert", "There could be a crash. Running machine learning model...", "OK");
                    });

                    // Calculate the correlation of recent data
                    var correlation = CalculateCorrelation();
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        // Alert the user about potential crash
                        await DisplayAlert("Alert", $"Potential crash detected! Correlation: {correlation}", "OK");

                        // Use the ML model to confirm the crash
                        bool isCrash = await InvokeMachineLearningModel();
                        if (isCrash)
                        {
                            HandleModelResult(isCrash);
                        }
                    });
                }
            }
        }



        // Calculate correlation between accelerometer and gyroscope data
        private double CalculateCorrelation()
        {
            var recentData = sensorDataList.TakeLast(10).ToList();  // Use last 10 data points for correlation calculation
            if (recentData.Count < 2) return 0;

            double meanAccel = recentData.Average(d => d.AccelMag);
            double meanGyro = recentData.Average(d => d.GyroMag);
            double covariance = recentData.Sum(d => (d.AccelMag - meanAccel) * (d.GyroMag - meanGyro)) / recentData.Count;
            double stdDevAccel = Math.Sqrt(recentData.Sum(d => Math.Pow(d.AccelMag - meanAccel, 2)) / recentData.Count);
            double stdDevGyro = Math.Sqrt(recentData.Sum(d => Math.Pow(d.GyroMag - meanGyro, 2)) / recentData.Count);

            return covariance / (stdDevAccel * stdDevGyro);
        }



        // Invoke the machine learning model to predict a crash
        private async Task<bool> InvokeMachineLearningModel()
        {
            var inputData = PrepareModelInput(sensorDataList);
            if (inputData != null && predictionEngine != null)
            {
                var modelInput = new ModelInput { Features = inputData };
                var prediction = predictionEngine.Predict(modelInput);
                return prediction.PredictedLabel == 1; // Assuming 1 indicates a crash
            }
            else
            {
                await DisplayAlert("Error", "Failed to prepare input data for the model or the model session is not initialized.", "OK");
                return false;
            }
        }


        // Prepare sensor data for machine learning model input
        private float[]? PrepareModelInput(List<SensorData> data)
        {
            if (data == null || data.Count == 0)
            {
                return null;
            }

            float correlation = CalculateCorrelation(data);

            var flatData = data.Select(d => new float[]
            {
                (float)d.SmoothedAccelMag,
                (float)d.SmoothedGyroMag,
                (float)d.AccelChange,
                (float)d.GyroChange,
                correlation
            }).SelectMany(x => x).ToArray();

            return flatData;
        }

        private float CalculateCorrelation(List<SensorData> data)
        {
            // Placeholder: calculate and return correlation here
            // Simple example calculation (adjust based on actual needs)
            double meanAccel = data.Average(d => d.SmoothedAccelMag);
            double meanGyro = data.Average(d => d.SmoothedGyroMag);
            double covariance = data.Sum(d => (d.SmoothedAccelMag - meanAccel) * (d.SmoothedGyroMag - meanGyro)) / data.Count;
            double stdDevAccel = Math.Sqrt(data.Sum(d => Math.Pow(d.SmoothedAccelMag - meanAccel, 2)) / data.Count);
            double stdDevGyro = Math.Sqrt(data.Sum(d => Math.Pow(d.SmoothedGyroMag - meanGyro, 2)) / data.Count);

            return (float)(covariance / (stdDevAccel * stdDevGyro));
        }



        // Run the ONNX model with the prepared data
        private async Task<bool> RunModelAsync(float[] input)
        {
            if (predictionEngine == null)
            {
                await DisplayAlert("Error", "The model session is not initialized.", "OK");
                return false;
            }

            var modelInput = new ModelInput { Features = input };
            var prediction = predictionEngine.Predict(modelInput);

            var crashProbability = prediction.Probabilities[0];
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayAlert("Crash Probability", $"Crash detection confidence: {crashProbability:P2}", "OK");
            });
            return prediction.PredictedLabel == 1; // Assuming 1 indicates a crash
        }


        // Handle the result of the model prediction
        private void HandleModelResult(bool isCrash)
        {
            if (isCrash)
            {
                MainThread.BeginInvokeOnMainThread(() => {
                    DisplayAlert("Crash Confirmed", "A crash has been detected and confirmed.", "OK");
                });
            }
        }


        private async void TestModelWithSampleInput()
        {
            if (predictionEngine == null)
            {
                await DisplayAlert("Error", "The model session is not initialized.", "OK");
                return;
            }

            float[] sampleInputData = new float[] { 50.0f, 50.0f, 20.0f, 20.0f, 1.0f };
            var tensor = new DenseTensor<float>(sampleInputData, new[] { 1, 5 });

            bool isCrash = await RunModelAsync(sampleInputData);

            string message = isCrash ? "The model predicts a crash." : "The model does not predict a crash.";
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayAlert("Crash Test Result", message, "OK");
            });
        }

        // Handle button press for starting or stopping the activity
        private void OnStartStopClicked(object sender, EventArgs e)
        {
            if (isLongPress)
            {
                isLongPress = false;
                return;
            }

            if (!isRunning)
            {
                stopwatch.Start();
                isRunning = true;
                startStopButton.Text = "Stop";

                // Initialize sensors if not already initialized or reset them as needed
                if (!sensorsInitialized)
                {
                    InitializeSensors();
                }

                uiTimer = new System.Threading.Timer((state) =>
                {
                    Dispatcher.Dispatch(() =>
                    {
                        stopwatchTime.Text = stopwatch.Elapsed.ToString(@"hh\:mm\:ss\.fff");
                    });
                }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
            }
            else
            {
                StopActivity();
            }
        }

        // Update the UI with time data
        private void SetTime()
        {
            timeLable.Text = $"laps {time.Second}:{time.Millisecond:000}";
        }

        // Handle long button presses for emergency actions
        private async void OnButtonPressed(object sender, EventArgs e)
        {
            pressTime = DateTime.Now;
            await Task.Delay(4000);  // Wait for 4 seconds

            if ((DateTime.Now - pressTime).TotalSeconds >= 4)
            {
                isLongPress = true;
                // Call the HandleEmergency function
                await HandleEmergency();

            }
        }

        private async Task HandleEmergency()
        {
            // Play a loud sound
            await TextToSpeech.SpeakAsync("Alert! Emergency!");

            // Vibrate 
            int secondsToVibrate = Random.Shared.Next(1, 7);
            TimeSpan vibrationLength = TimeSpan.FromSeconds(secondsToVibrate);
            Vibration.Default.Vibrate(vibrationLength);

            await DisplayAlert("Emergency", "Emergency actions will be implemented here.", "OK");

            // Stop the vibration
            Vibration.Default.Cancel();

            // Send the current location to the phoneNumber viewModel
            await SendLocationViaSMS();
        }


        private async Task SendLocationViaSMS()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
                var location = await Geolocation.GetLocationAsync(request);

                if (location != null)
                {
                    // Format the location data
                    string locationText = $"Latitude: {location.Latitude}, Longitude: {location.Longitude}";

                    // Create an SMS message with the location data and the phoneNumber from the viewModel
                    var message = new SmsMessage(locationText, new[] { viewModel.PhoneNumber.Trim() });

                    // Check if SMS composition is supported on the device
                    if (Sms.Default.IsComposeSupported)
                    {
                        // Compose and send the SMS message
                        await Sms.Default.ComposeAsync(message);

                        // Optional: Announce the action for accessibility
                        SemanticScreenReader.Announce("SMS composed and sent");
                    }
                    else
                    {
                        // Display an alert if SMS composition is not supported on the device
                        await DisplayAlert("Not Supported", "SMS composition is not supported on this device.", "OK");
                    }
                }
                else
                {
                    // Handle case where location is null
                    // For example, display an error message or retry getting location
                    // await DisplayAlert("Location Error", "Unable to retrieve current location.", "OK");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to get location: {ex.Message}");
                // Handle exception
                // await DisplayAlert("Error", "Failed to get current location.", "OK");
            }
        }

        private void DisplayPhoneNumber()
        {
            // Access the PhoneNumber property from the view model and display it
            string firstName = viewModel.FirstName;
            string surName = viewModel.SurName;
            string phoneNumber = viewModel.PhoneNumber;

            PhoneNumberLabel.Text = "Recipent: " + firstName + " " + surName + "\n" + "phone number: " + phoneNumber;
        }
        private void OnButtonReleased(object sender, EventArgs e)
        {
            if ((DateTime.Now - pressTime).TotalSeconds < 4)
            {
                isLongPress = false;
            }
        }

        private void OnToggleCallToggled(object sender, ToggledEventArgs e)
        {
            ToggleCall(e.Value);
        }

        private void ToggleCall(bool shouldStart)
        {
            if (PhoneDialer.Default.IsSupported)
                PhoneDialer.Default.Open("999");
        }

        private async void OnSendClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(viewModel.PhoneNumber) || string.IsNullOrWhiteSpace(MessageBox.Text))
            {
                await DisplayAlert("Error", "Please enter both a recipient and a message.", "OK");
                return;
            }

            var message = new SmsMessage(MessageBox.Text.Trim(), new[] { viewModel.PhoneNumber.Trim() });

            if (Sms.Default.IsComposeSupported)
            {
                await Sms.Default.ComposeAsync(message);
                // Optional: Announce the button text for accessibility if needed
                SemanticScreenReader.Announce(SendBtn.Text);
            }
            else
            {
                await DisplayAlert("Not Supported", "SMS composition is not supported on this device.", "OK");
            }
        }
    }


    
}
