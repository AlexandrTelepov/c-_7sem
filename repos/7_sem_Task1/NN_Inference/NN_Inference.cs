using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using System.Numerics.Tensors;
using Microsoft.ML.Transforms;
using Microsoft.ML.Data;
using System.IO;
using System.Drawing;
using System.Threading;

namespace NN_Inference
{
    public class NN_Inferencer
    {
        public string modelPath { get; set; }
        public string dataPath { get; set; }

        public NN_Inferencer(string modelPath, string dataPath)
        {
            this.modelPath = modelPath;
            this.dataPath = dataPath;
        }

        public float[] forward()
        {
            float[] resData = new float[10];
            int[] resDimensions = { 10 };
            Tensor<float> lastResult = new DenseTensor<float>(resData, resDimensions);
            SessionOptions options = new SessionOptions();
            using (var session = new InferenceSession(modelPath))//, options))
            {
                var inputMeta = session.InputMetadata;
                var container = new List<NamedOnnxValue>();

                Bitmap img = Image.FromFile(dataPath) as Bitmap;
                float[] inputData = new float[1*28*28];
                for (int i = 0; i < img.Width; i++)
                {
                    for (int j = 0; j < img.Height; j++)
                    {
                        Color pixel = img.GetPixel(i, j);
                        inputData[i * img.Height + j] = pixel.R / 255.0f;

                    }
                }
                int[] dimensions = { 1, 1, 28, 28 };
                foreach (var name in inputMeta.Keys)
                {
                    var tensor = new DenseTensor<float>(inputData, dimensions);
                    container.Add(NamedOnnxValue.CreateFromTensor<float>(name, tensor));
                }
                // Run the inference
                using (var results = session.Run(container))  // results is an IDisposableReadOnlyCollection<DisposableNamedOnnxValue> container
                {
                    // dump the results
                    foreach (var r in results)
                    {
                        lastResult = r.AsTensor<float>().Clone();
                    }
                }
            }
            return lastResult.ToArray<float>();
        }
    }
}
