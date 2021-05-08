﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Tensorflow.Binding;
using static Tensorflow.KerasApi;
using static PandasNet.PandasApi;
using System.IO;
using SciSharp.Models.TimeSeries;

namespace Models.Run
{
    /// <summary>
    /// https://www.tensorflow.org/tutorials/structured_data/time_series
    /// </summary>
    public class SampleTimeSeries
    {
        public void Run()
        {
            var zip_path = keras.utils.get_file("jena_climate_2009_2016.csv.zip",
                "https://storage.googleapis.com/tensorflow/tf-keras-datasets/jena_climate_2009_2016.csv.zip",
                cache_subdir: "jena_climate_2009_2016",
                extract: true);

            var df = pd.read_csv(Path.Combine(zip_path, "jena_climate_2009_2016.csv"));
            df = df[5, 6];
            var date_time = pd.to_datetime(df.pop("Date Time"), "dd.MM.yyyy HH:mm:ss");
            print(df.head());
            print(df.describe().transpose());

            // Wind velocity
            var wv = df["wv (m/s)"];
            var bad_wv = wv == -9999.0f;
            wv[bad_wv] = 0.0f;

            var max_wv = df["max. wv (m/s)"];
            var bad_max_wv = max_wv == -9999.0f;
            max_wv[bad_max_wv] = 0.0f;

            // The above inplace edits are reflected in the DataFrame
            print(df["wv (m/s)"].min());

            // convert the wind direction and velocity columns to a wind vector
            wv = df.pop("wv (m/s)");
            max_wv = df.pop("max. wv (m/s)");

            // Convert to radians.
            var wd_rad = df.pop("wd (deg)") * pd.pi / 180;

            // Calculate the wind x and y components.
            df["Wx"] = wv * pd.cos(wd_rad);
            df["Wy"] = wv * pd.sin(wd_rad);

            // Calculate the max wind x and y components.
            df["max Wx"] = max_wv * pd.cos(wd_rad);
            df["max Wy"] = max_wv * pd.sin(wd_rad);

            var timestamp_s = date_time.map<DateTime, float>(pd.timestamp);

            var day = 24 * 60 * 60;
            var year = 365.2425f * day;
            df["Day sin"] = pd.sin(timestamp_s * (2 * pd.pi / day));
            df["Day cos"] = pd.cos(timestamp_s * (2 * pd.pi / day));
            df["Year sin"] = pd.sin(timestamp_s * (2 * pd.pi / year));
            df["Year cos"] = pd.cos(timestamp_s * (2 * pd.pi / year));

            // Split the data
            var column_indices = enumerate(df.columns).Select(x => new
            {
                Index = x.Item1,
                x.Item2.Name
            }).ToArray();

            var n = df.shape[0];
            var num_features = df.shape[1];
            var train_df = df[pd.int32(n * 0.7)];
            var val_df = df[pd.int32(n * 0.7), pd.int32(n * 0.9), 1];
            var test_df = df[pd.int32(n * 0.9), 1];

            // Normalize the data
            var train_mean = train_df.mean();
            var train_std = train_df.std();

            train_df = (train_df - train_mean) / train_std;
            val_df = (val_df - train_mean) / train_std;
            test_df = (test_df - train_mean) / train_std;

            var w1 = new WindowGenerator(input_width: 24, label_width: 1, shift: 24,
                train_df: train_df, val_df: val_df, test_df: test_df,
                label_columns: new[] { "T (degC)" });

            var w2 = new WindowGenerator(input_width: 6, label_width: 1, shift: 1,
                train_df: train_df, val_df: val_df, test_df: test_df,
                label_columns: new[] { "T (degC)" });

            var array1 = pd.array<float>(train_df[w2.total_window_size]);
            var array2 = pd.array<float>(train_df[100, 100 + w2.total_window_size, 1]);
            var array3 = pd.array<float>(train_df[200, 200 + w2.total_window_size, 1]);
            var example_window = tf.stack(new[]
            {
                tf.constant(array1),
                tf.constant(array2),
                tf.constant(array3)
            });

            var (example_inputs, example_labels) = w2.SplitWindow(example_window);
            print("All shapes are: (batch, time, features)");
            print($"Window shape: {example_window.TensorShape}");
            print($"Inputs shape: {example_inputs.TensorShape}");
            print($"Labels shape: {example_labels.TensorShape}");

            w2.GetTrainingDataset();
        }
    }
}
