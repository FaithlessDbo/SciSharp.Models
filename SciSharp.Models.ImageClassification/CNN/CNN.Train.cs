﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Tensorflow;
using static Tensorflow.Binding;

namespace SciSharp.Models.ImageClassification
{
    public partial class CNN
    {
        Tensor loss, accuracy, cls_prediction;
        Operation optimizer;
        public void Train(TrainingOptions options)
        {
            var graph = BuildGraph(options);
            using var sess = tf.Session(graph);

            // Number of training iterations in each epoch
            var num_tr_iter = len(options.TrainingData.Features) / options.BatchSize;
            var epochs = options.Epochs;
            var batch_size = options.BatchSize;
            var (x_train, y_train) = (options.TrainingData.Features, options.TrainingData.Labels);
            var (x_valid, y_valid) = (options.ValidationData.Features, options.ValidationData.Labels);
            var init = tf.global_variables_initializer();
            sess.run(init);

            float loss_val = 100.0f;
            float accuracy_val = 0f;

            var sw = new Stopwatch();
            sw.Start();
            foreach (var epoch in range(epochs))
            {
                print($"Training epochs: {epoch + 1}/{epochs}");
                // Randomly shuffle the training data at the beginning of each epoch 
                (x_train, y_train) = Randomize(x_train, y_train);

                foreach (var iteration in range(num_tr_iter))
                {
                    var start = iteration * batch_size;
                    var end = (iteration + 1) * batch_size;
                    var (x_batch, y_batch) = GetNextBatch(x_train, y_train, start, end);

                    // Run optimization op (backprop)
                    sess.run(optimizer, (x, x_batch), (y, y_batch));

                    if (iteration % display_freq == 0)
                    {
                        // Calculate and display the batch loss and accuracy
                        (loss_val, accuracy_val) = sess.run((loss, accuracy), new FeedItem(x, x_batch), new FeedItem(y, y_batch));
                        print($"iter {iteration.ToString("000")}: Loss={loss_val.ToString("0.0000")}, Training Accuracy={accuracy_val.ToString("P")} {sw.ElapsedMilliseconds}ms");
                        sw.Restart();
                    }
                }

                // Run validation after every epoch
                (loss_val, accuracy_val) = sess.run((loss, accuracy), (x, x_valid), (y, y_valid));
                print("---------------------------------------------------------");
                print($"Epoch: {epoch + 1}, validation loss: {loss_val.ToString("0.0000")}, validation accuracy: {accuracy_val.ToString("P")}");
                print("---------------------------------------------------------");
            }

            SaveCheckpoint(sess);
        }

        void SaveCheckpoint(Session sess)
        {
            var saver = tf.train.Saver();
            saver.save(sess, Path.Combine("", "mnist_cnn.ckpt"));
        }
    }
}
