﻿using CNTK;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace ki
{
    class CalculateData
    {
        const double divident = 10000;
        const int x = 210;
        static DB dB = null;
        int categorycount;
        public CalculateData()
        {
            try
            {
                dB = new DB();
            }
            catch (Exception)
            {

                Console.WriteLine("Connection to DB failed");
            }
        }
        public enum Activation
        {
            None,
            ReLU,
            Sigmoid,
            Tanh
        }

        public static Model Train(MLContext mlContext, List<TwoInputRegressionModel> inputs)
        {

            // <Snippet6>
            IDataView dataView = mlContext.Data.LoadFromEnumerable<TwoInputRegressionModel>(inputs);
            // </Snippet6>
            IDataView trainData = mlContext.Data.TrainTestSplit(dataView).TrainSet;
            // <Snippet7>
            IEstimator<ITransformer> pipeline = mlContext.Transforms.CopyColumns(outputColumnName: "Label", inputColumnName: "Co2")
                    // </Snippet7>
                    // <Snippet8>

                    // </Snippet8>
                    // <Snippet9>
                    .Append(mlContext.Transforms.Concatenate("Features", "Year", "Population"))
                    // </Snippet9>
                    // <Snippet10>
                    .Append(mlContext.Regression.Trainers.Sdca());
            // </Snippet10>

            Console.WriteLine("1");

            Console.WriteLine("=============== Create and Train the Model ===============");

            // <Snippet11>
            ITransformer model = pipeline.Fit(dataView); //here it stops

            // </Snippet11>
            Console.WriteLine("2");
            Console.WriteLine("=============== End of training ===============");
            Console.WriteLine();
            // <Snippet12>
            return new Model(model, mlContext, dataView);
            // </Snippet12>
        }

        public Trainer createTrainer(Function network, Variable target)
        {
            //learning rate
            var lrate = 0.082;
            var lr = new TrainingParameterScheduleDouble(lrate);
            //network parameters
            var zParams = new ParameterVector(network.Parameters().ToList());

            //create loss and eval
            Function loss = CNTKLib.SquaredError(network, target);
            Function eval = CNTKLib.SquaredError(network, target);

            //learners
            //
            var llr = new List<Learner>();
            var msgd = Learner.SGDLearner(network.Parameters(), lr);
            llr.Add(msgd);

            //trainer
            var trainer = Trainer.CreateTrainer(network, loss, eval, llr);
            //
            return trainer;
        }

        public async Task<List<Countrystats>> GenerateForEachCountryAsync(List<int> laenderIDs, List<int> kategorienIDs, int from, int futureYear)
        {
            List<string> laender = await dB.GetCountriesToCategoriesAsync(laenderIDs); //Liste mit allen Ländern
            List<Countrystats> countrystats = new List<Countrystats>();
            for (int i = 0; i < laender.Count; i++)
            {
                await Task.Run(async () =>
                 {
                     Countrystats c = new Countrystats();
                     c.Country = new Country(laender[i]);
                     c.ListWithCategoriesWithYearsAndValues = await GenerateAsync(laender[i], kategorienIDs, from, futureYear);
                     countrystats.Add(c);

                 });

                Console.WriteLine("Land {0} wurde berechnet", laender[i]);
            }
            return countrystats;
        }
        /// <summary>
        /// Calculates future values based on alreadyknown Parameters
        /// </summary>
        /// <param name="yearWithValues">List that gets extended</param>
        /// <param name="from">startyear</param>
        /// <param name="futureYear">year in the future</param>
        /// <param name="parStor">parameter</param>
        /// <returns></returns>
        public List<YearWithValue> Predict(List<YearWithValue> yearWithValues, int from, int futureYear, ParameterStorage parStor)
        {
            double j = yearWithValues.Max(k => k.Year);
            int valueToDiv = Convert.ToInt32(CategoriesWithYearsAndValues.GetValuesFromList(yearWithValues).Max());
            float[] inputs = CategoriesWithYearsAndValues.GetYearsFromList(yearWithValues);
            List<double> listForTheNormalizedInputs = new List<double>();
            foreach (var item in inputs)
            {
                listForTheNormalizedInputs.Add(item); //Small brain schleife?
            }
            Input input = StandardizationYears(listForTheNormalizedInputs, futureYear);
            inputs = input.getAlleJahreNormiert();
            if (j < futureYear)
            {
                float inputsMax = inputs.Max();
                while (j < futureYear)
                {
                    j++;
                    yearWithValues.Add(new YearWithValue(j, new Wert(Convert.ToDecimal(parStor.W * inputsMax + parStor.b) * valueToDiv)));
                    float[] inputtemp = CategoriesWithYearsAndValues.GetYearsFromList(yearWithValues);
                    List<double> fuckinghelpme = new List<double>();
                    foreach (var item in inputtemp)
                    {
                        fuckinghelpme.Add(item); //Small brain schleife?
                    }
                    Input input2 = StandardizationYears(fuckinghelpme, futureYear);
                    inputtemp = input2.getAlleJahreNormiert();
                    inputsMax = inputtemp.Max();
                }
            }
            else //cut list from year to futureyear
            {
                if (futureYear > from)
                {
                    int indexMax = yearWithValues.FindIndex(a => a.Year == Convert.ToInt32(futureYear)); //finde Index von Jahr bis zu dem man Daten braucht
                    yearWithValues.RemoveRange(indexMax, yearWithValues.Count - indexMax); //Cutte List von Jahr bis zu dem man es braucht bis Ende

                    int indexMin = yearWithValues.FindIndex(b => b.Year == Convert.ToInt32(from));
                    yearWithValues.RemoveRange(0, indexMin);
                }
                else
                {
                    var temp = yearWithValues.Where(x => x.Year == from);
                    yearWithValues = temp.ToList(); ;
                }
            }
            return yearWithValues;
        }

        private static Function applyActivationFunction(Function layer, Activation actFun)
        {
            switch (actFun)
            {
                default:
                case Activation.None:
                    return layer;
                case Activation.ReLU:
                    return CNTKLib.ReLU(layer);
                case Activation.Sigmoid:
                    return CNTKLib.Sigmoid(layer);
                case Activation.Tanh:
                    return CNTKLib.Tanh(layer);
            }
        }

        private static void Evaluate(MLContext mlContext, ITransformer model, List<YearWithValue> list)
        {
            IDataView data = mlContext.Data.LoadFromEnumerable<YearWithValue>(list);
            var predictions = model.Transform(data);
            var metrics = mlContext.Regression.Evaluate(predictions, "Label", "Score");

            Console.WriteLine($"*       RSquared Score:      {metrics.RSquared:0.##}");

            Console.WriteLine($"*       Root Mean Squared Error:      {metrics.RootMeanSquaredError:#.##}");
        }

        /// <summary>
        /// Gibt eine Tulpe mit Inputs und Outputs zurück
        /// </summary>
        /// <param name="ListOfListOfYearWithValue">Liste mit Listen von Inputs und Outputs</param>
        /// <param name="InputNumber">Anzahl Inputs</param>
        /// <param name="OutputNumber">Anzahl Outputs</param>
        /// <returns></returns>
        static (float[], float[]) LoadData(List<YearWithValue> InputList, List<YearWithValue> OutputList, int InputNumber, int OutputNumber)
        {
            var features = new List<float>();
            var label = new List<float>();
            //Überprüfe, ob beide Kategorien gleich viele Einträge haben
            if (InputList.Count != OutputList.Count)
            {


                //Meier

                //TODO: Methode schreiben die Listen so cuttet dass beide in den gleichen Jahren einen Eitnrag haben
                if (InputList.Count > OutputList.Count)
                {
                    var result = OutputList.Join(InputList, element1 => element1.Year, element2 => element2.Year, (element1, element2) => element1);
                }

                else
                {
                    var result = InputList.Join(OutputList, element1 => element1.Year, element2 => element2.Year, (element1, element2) => element1);

                }
                LoadData(InputList, OutputList, InputNumber, OutputNumber);
            }
            else
            {
                var length = InputList.Count;

                //Für jedes Element in den beiden Listen
                for (int i = 0; i < length; i++)
                {
                    float[] input = new float[InputNumber];
                    for (int j = 0; j < InputNumber - 1; j++)
                    {
                        input[j] = InputList[i].Value.value;
                        input[j + 1] = InputList[i].Year;
                    }
                    float[] output = new float[OutputNumber];
                    for (int k = 0; k < OutputNumber; k++)
                    {
                        output[k] = OutputList[i].Value.value;
                    }
                    features.AddRange(input);
                    label.AddRange(output);
                }

            }
            return (features.ToArray(), label.ToArray());


        }

        private static async Task<List<YearWithValue>> PredictCo2OverYearsAsync(Model modelContainer, int futureYear, int coa_id, List<YearWithValue> emissions)
        {
            //Get Population till future year
            List<YearWithValue> population = await dB.GetPopulationByCoaIdAsync(coa_id); //get population that is known 

            if (CompareBiggestValueToFutureYear(population, futureYear))     //check if known population is enough to predict emission
            {
                population = await PredictPopulationAsync(coa_id, futureYear, population); //get population to predict emission
            }
            TwoInputRegressionModel[] populationData = new TwoInputRegressionModel[population.Count];
            for (int i = 0; i < populationData.Count(); i++)
            {
                populationData[i] = new TwoInputRegressionModel() { Year = population[i].Year, Population = population[i].Value.value };
            }
            PredictionEngine<TwoInputRegressionModel, TwoInputRegressionPrediction> predictionEngine = modelContainer.mLContext.Model.CreatePredictionEngine<TwoInputRegressionModel, TwoInputRegressionPrediction>(modelContainer.trainedModel);
            IDataView inputData = modelContainer.mLContext.Data.LoadFromEnumerable(populationData);
            IDataView predictions = modelContainer.trainedModel.Transform(inputData);
            float[] scoreColumn = predictions.GetColumn<float>("Score").ToArray();
            //Use PredictCo2
            float max = emissions.Max(v => v.Year);
            while (max < futureYear)
            {
                max++;
                emissions.Add(PredictCo2(modelContainer.mLContext, modelContainer.trainedModel, max, population.First(p => p.Year == max).Value.value));
            }
            return emissions;

        }
        private static YearWithValue PredictCo2(MLContext mlContext, ITransformer model, float year, float population)
        {
            var predictionFunction = mlContext.Model.CreatePredictionEngine<TwoInputRegressionModel, TwoInputRegressionPrediction>(model);
            var test = new TwoInputRegressionModel() { Year = year, Population = population };
            var prediction = predictionFunction.Predict(test);
            return new YearWithValue(year, new Wert(prediction.Co2, true));
        }

        private static void printTrainingProgress(Trainer trainer, int minibatchIdx, int outputFrequencyInMinibatches)
        {
            if ((minibatchIdx % outputFrequencyInMinibatches) == 0 && trainer.PreviousMinibatchSampleCount() != 0)
            {
                float trainLossValue = (float)trainer.PreviousMinibatchLossAverage();
                float evaluationValue = (float)trainer.PreviousMinibatchEvaluationAverage();
                Console.WriteLine($"Minibatch: {minibatchIdx} CrossEntropyLoss = {trainLossValue}, EvaluationCriterion = {evaluationValue}");
            }
        }

        private static Function simpleLayer(Function input, int outputDim, DeviceDescriptor device)
        {
            //prepare default parameters values
            var glorotInit = CNTKLib.GlorotUniformInitializer(
                    CNTKLib.DefaultParamInitScale,
                    CNTKLib.SentinelValueForInferParamInitRank,
                    CNTKLib.SentinelValueForInferParamInitRank, 1);

            //
            var var = (Variable)input;
            var shape = new int[] { outputDim, var.Shape[0] };
            var weightParam = new Parameter(shape, DataType.Float, glorotInit, device, "w");
            var biasParam = new Parameter(new NDShape(1, outputDim), 0, device, "b");


            return CNTKLib.Times(weightParam, input) + biasParam;

        }

        private Function createFFNN(Variable input, int hiddenLayerCount, int hiddenDim, int outputDim, Activation activation, string modelName, DeviceDescriptor device)
        {
            //First the parameters initialization must be performed
            var glorotInit = CNTKLib.GlorotUniformInitializer(
                    CNTKLib.DefaultParamInitScale,
                    CNTKLib.SentinelValueForInferParamInitRank,
                    CNTKLib.SentinelValueForInferParamInitRank, 1);

            //hidden layers creation
            //first hidden layer
            Function h = simpleLayer(input, hiddenDim, device);
            h = applyActivationFunction(h, activation);
            for (int i = 1; i < hiddenLayerCount; i++)
            {
                h = simpleLayer(h, hiddenDim, device);
                h = applyActivationFunction(h, activation);
            }
            //the last action is creation of the output layer
            var r = simpleLayer(h, outputDim, device);
            r.SetName(modelName);
            return r;
        }

        private Function createLRModel(Variable x, DeviceDescriptor device)
        {
            //initializer for parameters
            var initV = CNTKLib.GlorotUniformInitializer(1.0, 1, 0, 1);

            //bias
            var b = new Parameter(new NDShape(1, 1), DataType.Float, initV, device, "b"); ;

            //weights
            var W = new Parameter(new NDShape(2, 1), DataType.Float, initV, device, "w");

            //matrix product
            var Wx = CNTKLib.Times(W, x, "wx");

            //layer
            var l = CNTKLib.Plus(b, Wx, "wx_b");

            return l;
        }

        private int DifferentValuesCount(List<YearWithValue> values)
        {
            return values.Distinct().Count();
        }

        private async Task<List<CategoriesWithYearsAndValues>> GenerateAsync(string country, List<int> kategorienIDs, int from, int futureYear)
        {

            Countrystats countrystats = new Countrystats(); //Klasse für alle Kategorien und deren Werte per Jahr
            countrystats.Country = new Country(country); //Land zu dem die Kategorien mit Werte gehören
            countrystats.ListWithCategoriesWithYearsAndValues = await dB.GetCategoriesWithValuesAndYearsAsync(country, kategorienIDs); //Werte mit Jahren
            categorycount = countrystats.ListWithCategoriesWithYearsAndValues.Count; //wie viele kategorien an daten für dieses land existieren
            List<CategoriesWithYearsAndValues> CategorysWithFutureValues = new List<CategoriesWithYearsAndValues>();
            Task<List<YearWithValue>>[] liste = new Task<List<YearWithValue>>[categorycount]; //liste damit jede kategorie in einem task abgearbeitet werden kann
            List<YearWithValue> PopulationTotal = new List<YearWithValue>();

            //Arbeite jede Kategorie parallel ab
            for (int i = 0; i < categorycount; i++)
            {
                //Erstelle für jede Kategorie einen Liste mit eigenen Datensätzen
                List<YearWithValue> SingleCategoryData = new List<YearWithValue>();

                //Hole einzelne Datensätze für jedes Jahr heraus
                foreach (var YearWithValue in countrystats.ListWithCategoriesWithYearsAndValues[i].YearsWithValues)
                {
                    SingleCategoryData.Add(new YearWithValue(YearWithValue.Year, new Wert(Convert.ToDecimal(YearWithValue.Value.value)), countrystats.Country.name, YearWithValue.cat_id));
                }
                //Wenn ein Wert nicht dokumentiert ist, ist in der Datenbank 0 drin. Das verfälscht den Wert für die Ki
                //entferne deswegen 0
                SingleCategoryData = RemoveZero(SingleCategoryData);
                //Wenn es mindestens ein Jahr einer Kategorie gibt, in der der Wert nicht 0 ist
                if (SingleCategoryData.Count > 1)
                {
                    int coaid = await dB.GetCountryByNameAsync(country); //numeric of country
                    int categ = await dB.GetCategoryByNameAsync(countrystats.ListWithCategoriesWithYearsAndValues[i].category); //numeric of category
                    //Bearbeite eigenen Datensatz
                    int multi = Scale(SingleCategoryData) - 1; //wie viel man die normierten werte mulitplizieren muss damit sie wieder echt sind

                    if (SingleCategoryData.Any(x => x.cat_id == 4))
                    {
                        PopulationTotal = SingleCategoryData;
                    }
                    if (DifferentValuesCount(SingleCategoryData) > 2)
                    {
                        //linear train
                        liste[i] = Task.Run(async () =>
                       {
                           if (SingleCategoryData.Any(x => x.cat_id > 38 && x.cat_id < 46)) //if categoy is an emission-type
                           {
                               if (dB.CheckModel(coaid, categ)) //check for model
                               {
                                   Model modelContainer = dB.LoadModel(coaid, categ);
                                   List<YearWithValue> yearWithValues = await PredictCo2OverYearsAsync(modelContainer, futureYear, coaid, SingleCategoryData);
                                   return yearWithValues;

                               }
                               else //calculate model
                               {
                                   List<YearWithValue> x = await TrainLinearMoreInputsMLNETAsync(SingleCategoryData, PopulationTotal, futureYear);
                                   return x;
                               }


                           }
                           else
                           {
                               //Wenn es mindestens ein Jahr einer Kategorie gibt, in der der Wert nicht 0 ist

                               bool parameterExists = await dB.CheckParametersAsync(coaid, categ); //check if parameter for this country and this category exist
                               if (parameterExists)
                               {
                                   Console.WriteLine("Daten werden von Datenbank genommen");
                                   ParameterStorage parStor = await dB.GetParameterAsync(coaid, categ); //Bekomme Parameter

                                   List<YearWithValue> yearWithValues = new List<YearWithValue>();
                                   foreach (var item in countrystats.ListWithCategoriesWithYearsAndValues[i - 1].YearsWithValues)
                                   {
                                       yearWithValues.Add(new YearWithValue(item.Year, new Wert(Convert.ToDecimal(item.Value.value)), countrystats.Country.name, item.cat_id));
                                   }
                                   yearWithValues = RemoveZero(yearWithValues);
                                   yearWithValues = Predict(yearWithValues, from, futureYear, parStor);
                                   return yearWithValues;

                               }
                               else
                               {
                                   if (SingleCategoryData.Any(x => x.cat_id == 4))
                                   {
                                       PopulationTotal = await TrainLinearOneOutputAsync(SingleCategoryData, futureYear, multi);
                                       return PopulationTotal;

                                   }
                                   else
                                   {
                                       List<YearWithValue> x = await TrainLinearOneOutputAsync(SingleCategoryData, futureYear, multi);
                                       return x;

                                   }
                               }
                           }

                           // 
                       });
                    }
                    else
                    {
                        liste[i] = Task<List<YearWithValue>>.Run(() =>
                        {
                            return TrainSigmoid(SingleCategoryData, futureYear, multi);
                        });
                    }
                }


                //ohne dieses else gäbe es einige leere Tasks im Array -> Exception
                //ohne if geht die KI datensätze ohne einträge durch -> Verschwendung von Rechenleistung und Zeit
                else
                {
                    liste[i] = Task.Run(() => { return new List<YearWithValue>(); });
                }
            }

            //Warte parallel bis alle Kategorien gelernt und berechnet wurden
            Task.WaitAll(liste);
            //returne alle Kategorien 
            for (int i = 0; i < categorycount; i++)
            {
                CategorysWithFutureValues.Add(new CategoriesWithYearsAndValues(countrystats.ListWithCategoriesWithYearsAndValues[i].category, liste[i].Result));
            }

            return CategorysWithFutureValues;


        }
        //Findet die letzten n höchsten Werte, also zB n= 5 in einem Array mit 10 Zahlen gibt die Zahlen von 5-10 zurück
        private float[] GetLastNValues(float[] array, int n, double step)
        {
            int count = array.Count();
            int temp = n;
            float[] f = new float[n];
            for (int i = count - 1; i > count - n; i--)
            {
                f[temp - 1] = array[i];
                temp--;
            }
            f[0] = float.Parse(Convert.ToString(f[1] - step));
            return f;
        }

        List<YearWithValue> RemoveZero(List<YearWithValue> collection)
        {
            var temp = collection.Where(i => i.Value.value != 0).ToList();
            return temp;
        }

        int Scale(List<YearWithValue> n)
        {
            double temp = Convert.ToDouble(n.Max(i => i.Value.value));
            int m = 1;
            while (1 <= temp)
            {
                temp = temp / 10;
                m++;
            }
            return m;
        }

    static   Input StandardizationYears(List<double> inputs, int Zukunftsjahr)
        {
            Input input = new Input();
            inputs = inputs.Distinct().ToList(); //Ich weiß dass ich ein Hashset verwenden könnte, aber ich weiß nicht ob sich das von der Performance lohnt. Add in Hashset = braucht länger als liste, dafür konsumiert liste.distinct zeit
            double maxvalue = inputs.Max();
            double count = inputs.Count;
            double diff = Zukunftsjahr - maxvalue;
            if (diff < 0)
            {
                diff = diff * 2;
            }
            double step = 1 / (count + diff);
            List<double> normierteWerte = new List<double>();
            input.step = step;
            double i = 0;
            foreach (var item in inputs)
            {
                input.AddJahr(item, i);
                i = i + step;
            }
            return input;
        }
      static  Input StandardizationValues(List<double> inputs, int Zukunftsjahr)
        {
            Input input = new Input();  
            double count = inputs.Count;
            double step = 1 / (count);
            List<double> normierteWerte = new List<double>();
            input.step = step;
            double i = 0;
            foreach (var item in inputs)
            {
                    input.AddWert(item, i);
                    i = i + step;  
               
            }
            return input;
        }
        static Input Standarization(List<YearWithValue> inputs, int Zukunftsjahr)
        {
            Input input = new Input();
            List<double> years = new List<double>();
            List<double> values = new List<double>();
            foreach (var item in inputs)
            {
                years.Add(item.Year);
                values.Add(item.Value.value);
            }
            Input inputYears = StandardizationYears(years, Zukunftsjahr);
            Input inputValues = StandardizationValues(values, Zukunftsjahr);
            input.SetYearsWithNorm(inputYears.GetYearsDic());
            input.SetValuesWithNorm(inputValues.GetValuesDic());
            return input;
        }

        private List<YearWithValue> TrainLinearMoreInputs(List<List<YearWithValue>> ListWithKnownValues, int FutureYear, int multi)
        {
            var device = DeviceDescriptor.UseDefaultDevice();


            //Network definition
            //Inputs sind Werte jeder Liste + das Jahr, zB wenn Liste 1 im Jahr 2015 den Wert 3 und Liste 2 den Wert 12 hat, wird der Input 2015, 3 und der Output 12 sein
            int InputsCount = ListWithKnownValues.Count; //anzahl der input parameter
            int OutputsCount = 1; //wie viele outputs raus kommen
            int numHiddenLayers = 1; //anzahl der hidden layer
            int hidenLayerDim = 6; //wie viele "knoten" der hidden layer hat


            //load data in to memory
            var dataSet = LoadData(ListWithKnownValues[0], ListWithKnownValues[1], InputsCount, OutputsCount);

            // build a NN model
            //define input and output variable
            var xValues = Value.CreateBatch<float>(new NDShape(1, InputsCount), dataSet.Item1, device);
            var yValues = Value.CreateBatch<float>(new NDShape(1, OutputsCount), dataSet.Item2, device);

            // build a NN model
            //define input and output variable and connecting to the stream configuration
            var feature = Variable.InputVariable(new NDShape(1, InputsCount), DataType.Float);
            var label = Variable.InputVariable(new NDShape(1, OutputsCount), DataType.Float);

            //Combine variables and data in to Dictionary for the training
            var dic = new Dictionary<Variable, Value>();
            dic.Add(feature, xValues);
            dic.Add(label, yValues);

            //Build simple Feed Froward Neural Network model
            // var ffnn_model = CreateMLPClassifier(device, numOutputClasses, hidenLayerDim, feature, classifierName);
            var ffnn_model = createFFNN(feature, numHiddenLayers, hidenLayerDim, OutputsCount, Activation.Tanh, "IrisNNModel", device);

            //Loss and error functions definition
            var trainingLoss = CNTKLib.CrossEntropyWithSoftmax(new Variable(ffnn_model), label, "lossFunction");
            var classError = CNTKLib.ClassificationError(new Variable(ffnn_model), label, "classificationError");

            // set learning rate for the network
            var learningRatePerSample = new TrainingParameterScheduleDouble(0.001125, 1);

            //define learners for the NN model
            var ll = Learner.SGDLearner(ffnn_model.Parameters(), learningRatePerSample);

            //define trainer based on ffnn_model, loss and error functions , and SGD learner
            var trainer = Trainer.CreateTrainer(ffnn_model, trainingLoss, classError, new Learner[] { ll });

            //Preparation for the iterative learning process
            //used 800 epochs/iterations. Batch size will be the same as sample size since the data set is small
            int epochs = 800;
            int i = 0;
            while (epochs > -1)
            {

                trainer.TrainMinibatch(dic, false, device);

                //print progress
                printTrainingProgress(trainer, i++, 50);

                //
                epochs--;
            }
            //Summary of training
            double acc = Math.Round((1.0 - trainer.PreviousMinibatchEvaluationAverage()) * 100, 2);

            Console.WriteLine($"------TRAINING SUMMARY--------");
            Console.WriteLine($"The model trained with the accuracy {acc}%");
            return new List<YearWithValue>();
            //  return KnownValues;
        }

        //für alle möglichen gase
        private async Task<List<YearWithValue>> TrainLinearMoreInputsMLNETAsync(List<YearWithValue> ListWithCO, List<YearWithValue> Population, int FutureYear)
        {
            MLContext mlContext = new MLContext(seed: 0);
            ListWithCO = ListWithCO.Distinct().ToList();
            int coaid = await dB.GetCountryByNameAsync(ListWithCO.First(x => x.Name != null).Name);      //Inshallah ist in dieser liste nie kein name irgendwo
            int catid = ListWithCO.First(x => x.cat_id != 0).cat_id;
            List<TwoInputRegressionModel> inputs = new List<TwoInputRegressionModel>();
            if (!(Population.Count > 0)) //ohje
            {
                Console.WriteLine("Zu diesem Punkt im Programm sollte es eigentlich nie kommen. Ich hab aber keine Zeit, das ordentlich zu fixen. Darum hier diese Pfusch-Lösung mit dieser Ausgabe als Erinnerung, dass ich das gscheid behebe, wenn noch Zeit überbleibt");
                Population = await dB.GetPopulationByCoaIdAsync(coaid);
            }
            foreach (var JahrMitCO in ListWithCO)
            {
                float tempyear = JahrMitCO.Year;
                foreach (var JahrMitPopulation in Population)
                {
                    if (JahrMitPopulation.Year == tempyear)
                    {
                        inputs.Add(new TwoInputRegressionModel() { Year = tempyear , Population = JahrMitPopulation.Value.value , Co2 = JahrMitCO.Value.value  });
                    }
                }
            }
            List<Input> input = NormalizeTwoInputRegressionParameters(inputs, FutureYear);
            float[] populationNorm = input.First(x => x.name == "population").getAlleWerteNormiert();
            float[] emissionNorm = input.First(x => x.name == "emission").getAlleWerteNormiert();
            float[] years = input[0].getAlleJahreNormiert();
            for (int i = 0; i < inputs.Count; i++)
            {
                inputs[i].Population = populationNorm[i];
                inputs[i].Co2 = emissionNorm[i];
                inputs[i].Year = years[i];
            }
            Model modelContainer = Train(mlContext, inputs);
            var model = modelContainer.trainedModel;
            double j = input[0].GetYearsDic().First(x => x.Value == inputs.Max(y => y.Year)).Key;
            if (j < FutureYear / 10000000)
            {

                j++;

                if (Population.Any(x => x.Year == j))
                {
                    //non-rekursives modell
                    //while (j < FutureYear)
                    //{

                    //    ListWithCO.Add(PredictCo2(mlContext, model, (float)j, Population.First(x => x.Year == j).Value.value));
                    //    j++;
                    //}
                    //rekursives trainieren
                    TwoInputRegressionModel[] populationData = new TwoInputRegressionModel[Population.Count];
                    for (int i = 0; i < Population.Count; i++)
                    {
                        populationData[i] = new TwoInputRegressionModel() {Population = Population[i].Value.value, Year = Population[i].Year};
                    }
                    IDataView inputData = mlContext.Data.LoadFromEnumerable(populationData);
                    IDataView predictions = model.Transform(inputData);
                    float[] scoreColumn = predictions.GetColumn<float>("Score").ToArray();

                    ListWithCO.Add(PredictCo2(mlContext, model, (float)j, Population.First(x => x.Year == j).Value.value));
                    return await TrainLinearMoreInputsMLNETAsync(ListWithCO, Population, FutureYear);

                }
                else     //Was tun falls keine Population in dem Jahr bekannt ist
                {
                    //Berechne Population bis zu gegebenem Zeitpunkt
                    //Schau ob Parameter zur Bevölkerung da sind

                    Population = await PredictPopulationAsync(coaid, FutureYear, Population);
                    //Dann berechnen
                    return await TrainLinearMoreInputsMLNETAsync(ListWithCO, Population, FutureYear);

                }




            }
            dB.SaveModel(modelContainer, coaid, catid);
            return ListWithCO;
        }
       private static List<Input> NormalizeTwoInputRegressionParameters(List<TwoInputRegressionModel> inputs, int futureYear)
        {
            List<YearWithValue> population = new List<YearWithValue>();
            List<YearWithValue> co2 = new List<YearWithValue>();
            foreach (var item in inputs)
            {
                population.Add(new YearWithValue(item.Year, new Wert(item.Population)));
        
                co2.Add(new YearWithValue(item.Year, new Wert(item.Co2), "emission"));
            }

          List<Input> list = new List<Input>() { Standarization(population, futureYear), Standarization(co2, futureYear )};
            list[0].name = "population";
            list[1].name = "emission";
            return list;

        }
        private static async Task<List<YearWithValue>> PredictPopulationAsync(int coaid, int futureYear, List<YearWithValue> population)
        {
            if (await dB.CheckParametersAsync(coaid, 4))
            {

                float m = population.Max(x => x.Year);
                ParameterStorage ps = await dB.GetParameterAsync(coaid, 4);
                while (m < futureYear)
                {

                    m++;
                    population.Add(new YearWithValue(m, new Wert(ps.W * m + ps.b)));

                }


            }
            else
            {
                //TODO: calculate parameter
            }
            return population;
        }
        private async Task<List<YearWithValue>> TrainLinearOneOutputAsync(List<YearWithValue> KnownValues, int FutureYear, int multi)
        {

            var device = DeviceDescriptor.UseDefaultDevice();
            ////Step 2: define values, and variables
            Variable x = Variable.InputVariable(new NDShape(1, 1), DataType.Float, "input");
            Variable y = Variable.InputVariable(new NDShape(1, 1), DataType.Float, "output");
            ////Step 2: define training data set from table above
            float[] inputs = CategoriesWithYearsAndValues.GetYearsFromList(KnownValues);
            List<double> temp = new List<double>();
            foreach (var item in inputs)
            {
                temp.Add(item); //Small brain schleife?
            }
            Input input = StandardizationYears(temp, FutureYear);
            inputs = input.getAlleJahreNormiert();
            float[] outputs = CategoriesWithYearsAndValues.GetValuesFromList(KnownValues);
            //Value.CreateBatch(Tensor(Achsen, Dimension), Werte, cpu/gpu)
            float[] outputsnormiert = new float[outputs.Count()];
            int WertZumDividieren = Convert.ToInt32(outputs.Max());
            for (int i = 0; i < outputs.Length; i++)
            {
                outputsnormiert[i] = outputs[i] / WertZumDividieren;
            }
            //Werte normiert lassen, sonst stackoverflow :>
            var xValues = Value.CreateBatch(new NDShape(1, 1), GetLastNValues(inputs, inputs.Length, input.step), device);
            var yValues = Value.CreateBatch(new NDShape(1, 1), GetLastNValues(outputsnormiert, outputs.Length, input.step), device);
            ////Step 3: create linear regression model
            var lr = createLRModel(x, device);
            ////Network model contains only two parameters b and w, so we query
            ////the model in order to get parameter values
            var paramValues = lr.Inputs.Where(z => z.IsParameter).ToList();
            var totalParameters = paramValues.Sum(c => c.Shape.TotalSize);
            ////Step 4: create trainer
            var trainer = createTrainer(lr, y);
            ////Ştep 5: training
            double b = 0, w = 0;
            int max = 2000;

            for (int i = 1; i <= max; i++)
            {
                var d = new Dictionary<Variable, Value>();
                d.Add(x, xValues);
                d.Add(y, yValues);
                //
                trainer.TrainMinibatch(d, true, device);
                //
                var loss = trainer.PreviousMinibatchLossAverage();
                var eval = trainer.PreviousMinibatchEvaluationAverage();
                //
                if (i % 200 == 0)
                    Console.WriteLine($"It={i}, Loss={loss}, Eval={eval}");

                if (i == max)
                {
                    //print weights
                    var b0_name = paramValues[0].Name;
                    var b0 = new Value(paramValues[0].GetValue()).GetDenseData<float>(paramValues[0]);
                    var b1_name = paramValues[1].Name;
                    var b1 = new Value(paramValues[1].GetValue()).GetDenseData<float>(paramValues[1]);
                    Console.WriteLine($" ");
                    Console.WriteLine($"Training process finished with the following regression parameters:");
                    Console.WriteLine($"b={b0[0][0]}, w={b1[0][0]}");
                    b = b0[0][0];
                    w = b1[0][0];
                    ParameterStorage ps = new ParameterStorage(float.Parse(w.ToString()), float.Parse(b.ToString()));
                    int coaid = await dB.GetCountryByNameAsync(KnownValues.Where(k => k.Name != null).First().Name);
                    await dB.SaveParameterAsync(ps, coaid, KnownValues.Where(k => k.cat_id != 0).First().cat_id, loss);
                    KnownValues = Predict(KnownValues, Convert.ToInt32(KnownValues.Min(k => k.Year)), FutureYear, ps);
                }
            }


            return KnownValues;
        }

        /// <summary>
        /// Sieht anhand einer Liste mit Jahren und Zahlen den Wert für Jahr Ziel voraus
        /// </summary>
        /// <param name="KnownValues">Liste bekannter Werte anhand deren die KI lernt</param>
        /// <param name="FutureYear"> Bis zu welchem Jahr die KI werte vorhersagen soll</param>
        /// <param name="multi">Wie viel man normierte Werte mulitplizieren muss</param>
        /// <returns>Liste mit allen bereits bekannten Werten + Vorhersagen für zukünftige Werte</returns>
        private List<YearWithValue> TrainSigmoid(List<YearWithValue> KnownValues, int FutureYear, int multi)
        {

            List<double> inputs = new List<double>(); //Jahre
            List<double> outputs = new List<double>(); //Werte 
                                                       //Für jedes Jahr mit Wert in der Liste mit Jahren und Werten d
            foreach (var YearWithValue in KnownValues)
            {
                inputs.Add(Convert.ToDouble(YearWithValue.Year));
                outputs.Add(Convert.ToDouble(YearWithValue.Value.value) / (Math.Pow(10, multi)));
            }
            Input input = StandardizationYears(inputs, FutureYear);
            Neuron hiddenNeuron1 = new Neuron();
            Neuron outputNeuron = new Neuron();
            hiddenNeuron1.randomizeWeights();
            outputNeuron.randomizeWeights();


            int lernvorgang = 0;
            int z = KnownValues.Count;
            //Trainiere alle bekannten Werte x mal
            while (lernvorgang < x)
            {

                for (int i = 0; i < z; i++)
                {
                    hiddenNeuron1.inputs = input.GetNormYear(inputs[i]);
                    outputNeuron.inputs = hiddenNeuron1.output;
                    outputNeuron.error = sigmoid.derived(outputNeuron.output) * (outputs[i] - outputNeuron.output);
                    outputNeuron.adjustWeights();
                    hiddenNeuron1.error = sigmoid.derived(hiddenNeuron1.output) * outputNeuron.error * outputNeuron.weights;
                    hiddenNeuron1.adjustWeights();
                }

                lernvorgang++;
            }

            //bekomme immer das höchste jahr
            double j = KnownValues.Max(i => i.Year);

            //wenn das höchste bekannte jahr kleiner ist als das Jahr, bis zu dem wir die Werte wissen wollen, 
            //dann füge das nächste Jahr als input ins Neuron, bekomme den Output und füge es in die Liste mit allen Werten ein
            //Dann Rekursion bis das größte Jahr nicht mehr kleiner ist als das Jahr bis zu dem wir rechnen wollen
            if (j < FutureYear)
            {
                hiddenNeuron1.inputs = input.GetNormYear(j) + input.step;
                outputNeuron.inputs = hiddenNeuron1.output;
                KnownValues.Add(new YearWithValue((Math.Round((inputs[inputs.Count - 1] + 1))), new Wert((float)(outputNeuron.output * Convert.ToDouble(Math.Pow(10, multi))), true)));
                return TrainSigmoid(KnownValues, FutureYear, multi);
            }
            //wenn alle Jahre bekannt sind, returne die Liste
            else
            {
                return KnownValues;
            }

        }
        /// <summary>
        /// checks, if the biggest year in a list of YearWithValue is bigger than the given value FutureYear
        /// not much code, but it appears often enough to outsource it
        /// </summary>
        /// <returns></returns>
        private static bool CompareBiggestValueToFutureYear(List<YearWithValue> yearWithValues, int futureYear)
        {
            return (yearWithValues.Max(v => v.Year) < futureYear);
        }
    }
}
