using Microsoft.ML;
using Microsoft.ML.Trainers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ki
{
    internal class DB
    {
        private static string ki_read_input = ConfigurationManager.ConnectionStrings["ki_read_input"].ConnectionString;
        private static string ki_read_output = ConfigurationManager.ConnectionStrings["ki_read_output"].ConnectionString;
        private static string ki_write_output = ConfigurationManager.ConnectionStrings["ki_write_output"].ConnectionString;

        /// <summary>
        /// Achtung: outdated
        /// Es wird bei jeder Methode neu connected und der richtige connectionstring verwendet
        /// Ist aber umst�ndlich das auszubessern, daher Konstruktor bitte lassen
        /// </summary>
        /// <param name="cs"></param>
        public DB()
        {
        }
        public async Task<int> GetMaxYearAsync(int coaID, int catID)
        {
            int max = 0;
            using (SqlConnection sql = new SqlConnection(ki_read_input))
            {
                await sql.OpenAsync();
                SqlCommand sqlCommand = sql.CreateCommand();
                sqlCommand.CommandText = $"SELECT MAX(year) AS max FROM input_data WHERE coa_id = {coaID} AND cat_id = {catID};";
                Console.WriteLine(sqlCommand.CommandText);
                using (SqlDataReader reader = await sqlCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        max = Convert.ToInt32(reader["max"]);
                    }
                }
            }
            return max;
        }
        public async Task<string> GetCountryNameByIDAsync(int coaId)
        {
            using (SqlConnection sql = new SqlConnection(ki_read_input))
            {
                await sql.OpenAsync();
                SqlCommand command = sql.CreateCommand();
                command.CommandText = $"SELECT name FROM country_or_area WHERE coa_id = '{coaId}'; ";
                Console.WriteLine(command.CommandText);
                string coa_name = "";
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        coa_name = Convert.ToString(reader["name"]);
                    }
                }
                return coa_name;
            }
        }
        public async Task<bool> CheckParametersAsync(int coaID, int catID)
        {
            using (SqlConnection sqlc = new SqlConnection(ki_read_output))
            {
                await sqlc.OpenAsync();
                SqlCommand command = sqlc.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) AS COUNT FROM output_data WHERE coa_id = {coaID} AND cat_id = {catID};";
                Console.WriteLine(command.CommandText);
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int count = (Int32)reader["COUNT"];
                        if (count > 0)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }

            return false;
        }
        /// <summary>
        /// Returns List of all Countries with Coordinates and name
        /// </summary>
        public async Task<List<Country>> GetAllCountriesAsync()
        {
            List<Country> countries = new List<Country>();
            using (SqlConnection sql = new SqlConnection(ki_read_input))
            {
                await sql.OpenAsync();
                SqlCommand command = sql.CreateCommand();
                command.CommandText = "SELECT * FROM country_or_area WHERE lat != 0;";
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int coa_id = Convert.ToInt32(reader["coa_id"]);
                        string coa_key = Convert.ToString(reader["coa_key"]);
                        string name = Convert.ToString(reader["name"]);
                        float lat = Convert.ToInt64(reader["lat"]);
                        float lon = Convert.ToInt64(reader["lon"]);
                        countries.Add(new Country(name, coa_key, lon, lat) { coa_id = coa_id });
                    }
                }
            }
            return countries;
        }


        /// <summary>
        /// Returns categorys with all years and values of a single country
        /// </summary>
        /// <param name="country">Country</param>
        /// <param name="kategorieIDs">List of category IDs</param>
        /// <returns>Liste mit Kategorien mit Jahren und Werten</returns>
        public async Task<List<CategoriesWithYearsAndValues>> GetCategoriesWithValuesAndYearsAsync(string country, List<int> kategorieIDs)
        {
            Console.WriteLine("GetCategoriesWithValuesAndYearsAsync");
            List<CategoriesWithYearsAndValues> keyValuePairs = new List<CategoriesWithYearsAndValues>();
            using (SqlConnection sqlConnection = new SqlConnection(ki_read_input))
            {
                await sqlConnection.OpenAsync();
                SqlCommand command = sqlConnection.CreateCommand();
                List<string> vs = await GetNamesOfCategoriesByIDsAsync(kategorieIDs);

                foreach (var item in vs)
                {
                    CategoriesWithYearsAndValues kmjw = new CategoriesWithYearsAndValues
                    {
                        category = new Category(item)
                    };
                    command.CommandText = $"SELECT year, ROUND(value,5) AS ROUND, c.cat_id FROM input_data JOIN category c on input_data.cat_id = c.cat_id JOIN country_or_area coa on input_data.coa_id = coa.coa_id WHERE c.name = '{item}' AND coa.name = '{country}';";
                    Console.WriteLine(command.CommandText);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        List<YearWithValue> temp = new List<YearWithValue>();
                        while (await reader.ReadAsync()) //fraglich ob es nicht eine bessere Methode gibt
                        {
                            int year = Convert.ToInt32(reader["year"]);
                            var value = Convert.ToDecimal(reader["ROUND"].ToString());
                            int catid = Convert.ToInt32(reader["cat_id"]);

                            temp.Add(new YearWithValue(year, new Wert((decimal)value, false), item, catid));
                        }
                        kmjw.YearsWithValues = temp;
                        keyValuePairs.Add(kmjw);
                    }
                }
            }

            return keyValuePairs;
        }

        public async Task<int> GetCategoryByNameAsync(string category)
        {
            Console.WriteLine("GetCategoryByName");
            using (SqlConnection sql = new SqlConnection(ki_read_input))
            {
                sql.Open();
                SqlCommand command = sql.CreateCommand();
                command.CommandText = $"SELECT cat_id FROM category WHERE name = '{category}'; ";
                Console.WriteLine(command.CommandText);
                int cat_id = 0;
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        cat_id = Convert.ToInt32(reader["cat_id"]);
                    }
                }
                return cat_id;
            }
        }

        public async Task<List<string>> GetCountriesToCategoriesAsync(List<int> coaids)
        {
            using (SqlConnection sqlc = new SqlConnection(ki_read_input))
            {
                await sqlc.OpenAsync();
                SqlCommand command = sqlc.CreateCommand();
                if (coaids.Count == 0)
                {
                    command.CommandText = "SELECT name FROM country_or_area;";
                }
                else
                {
                    List<string> statementConditions = new List<string>();

                    foreach (var item in coaids)
                    {
                        statementConditions.Add("coa_id=" + item);
                    }

                    command.CommandText = ConcatSQLConditions(statementConditions, "SELECT name FROM country_or_area") + ";";
                }
                List<string> land = new List<string>();
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) //fraglich ob es nicht eine bessere Methode gibt
                    {
                        land.Add((string)reader["name"]);
                    }
                }
                return land;
            }
        }

        /// <summary>
        /// Gets ID of a country by ISO code
        /// </summary>
        /// <param name="key">ISO</param>
        /// <returns></returns>
        public async Task<int> GetCountryByKeyAsync(string key)
        {
            Console.WriteLine("GetCountryByKey");
            int coa_id = 0;
            using (SqlConnection sqlc = new SqlConnection(ki_read_input))
            {
                await sqlc.OpenAsync();
                SqlCommand command = sqlc.CreateCommand();
                command.CommandText = $"SELECT coa_id FROM country_or_area WHERE coa_key='{key}'";
                Console.WriteLine(command.CommandText);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (await reader.ReadAsync())
                    {
                        coa_id = Convert.ToInt32(reader["coa_id"]);
                    }
                }
            }
            return coa_id;
        }

        public async Task<int> GetCountryByNameAsync(string name)
        {
            Console.WriteLine("GetCountryByName");
            using (SqlConnection sqlc = new SqlConnection(ki_read_input))
            {
                await sqlc.OpenAsync();
                SqlCommand command = sqlc.CreateCommand();
                command.CommandText = $"SELECT coa_id FROM country_or_area WHERE name = '{name}';";
                Console.WriteLine(command.CommandText);
                int coaid = -210;
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        coaid = Convert.ToInt32(reader["coa_id"]);
                    }
                }
                return coaid;
            }
        }

        public async Task<ParameterStorage> GetParameterAsync(int coaid, int catid)
        {
            Console.WriteLine("GetParameter");
            using (SqlConnection sqlc = new SqlConnection(ki_read_output))
            {
                await sqlc.OpenAsync();
                SqlCommand command = sqlc.CreateCommand();
                command.CommandText = $"SELECT value FROM output_data WHERE coa_id = {coaid} AND cat_id = {catid} ORDER BY error";
                Console.WriteLine(command.CommandText);
                float W = 0;
                float b = 0;
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string value = Convert.ToString(reader["value"]);
                        foreach (var parameterstring in value.Split(';'))
                        {
                            string[] singleParameters = parameterstring.Split('=');
                            if (singleParameters.Length > 1)
                            {
                                if (singleParameters[0].Contains("W"))
                                {
                                    W = float.Parse(singleParameters[1], CultureInfo.InstalledUICulture);
                                }
                                else
                                {
                                    b = float.Parse(singleParameters[1], CultureInfo.InstalledUICulture);
                                }
                            }
                        }
                    }
                }

                return new ParameterStorage(W, b);
            }
        }

        public async Task<List<YearWithValue>> GetPopulationByCoaIdAsync(int coaid)
        {
            List<YearWithValue> population = new List<YearWithValue>();
            using (SqlConnection sqlConnection = new SqlConnection(ki_read_input))
            {
                await sqlConnection.OpenAsync();
                SqlCommand sqlCommand = sqlConnection.CreateCommand();
                sqlCommand.CommandText = $"SELECT year, value FROM input_data WHERE cat_id = 4 AND coa_id = {coaid};";
                Console.WriteLine(sqlCommand.CommandText);
                using (SqlDataReader reader = await sqlCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        population.Add(new YearWithValue(Convert.ToDouble(reader["year"]), new Wert(Convert.ToDecimal(reader["value"]))));
                    }
                }
            }
            return population;
        }
        public void SaveModel(Model model, int coa_id, int cat_id)
        {
            model.mLContext.Model.Save(model.trainedModel, model.data.Schema, GenerateFileNameForModel(coa_id, cat_id));
            //not working
            //using (FileStream fs = new FileStream("test.onnx", FileMode.OpenOrCreate))
            //{
            //    Microsoft.ML.OnnxExportExtensions.ConvertToOnnx(model.mLContext.Model, model.trainedModel, model.data, fs);
            //}



        }
        public bool CheckModel(int coa_id, int cat_id)
        {
            return File.Exists(GenerateFileNameForModel(coa_id, cat_id));
        }
        public Model LoadModel(int coa_id, int cat_id)
        {
            MLContext mL = new MLContext();
            DataViewSchema schema;
            ITransformer predictionPipeline = mL.Model.Load(GenerateFileNameForModel(coa_id, cat_id), out schema);
            return new Model(predictionPipeline, mL);

        }
        public Model LoadTempModel()
        {
            return LoadModel(0, 77);
        }
        /// <summary>
        /// Das ist nur sehr wenig code, aber falls ich die Namenskonvention von den files �ndern m�chte/muss kann ich das direkt in einer Methode erledigen
        /// </summary>
        /// <returns></returns>
        private string GenerateFileNameForModel(int coa_id, int cat_id)
        {
            return $"{coa_id}-{cat_id}.zip";
        }
        public void SaveModelAsParameter(Model modelContainer, int coa_id, int cat_id, double loss)
        {
            ITransformer model = modelContainer.trainedModel;
            IEnumerable<ITransformer> chain = model as IEnumerable<ITransformer>;

            ISingleFeaturePredictionTransformer<object> predictionTransformer =
                chain.Last() as ISingleFeaturePredictionTransformer<object>;

            object modelParameters = predictionTransformer.Model;
            ParameterStorage ps = new ParameterStorage();
            Console.WriteLine();
        }
        public async Task SaveParameterAsync(ParameterStorage parameterStorage, int coa_id, int cat_id, double loss)
        {
            Console.WriteLine($"Parameter f�r {coa_id} und {cat_id} wird eingetragen");
            using (SqlConnection sql = new SqlConnection(ki_write_output))
            {
                await sql.OpenAsync();
                SqlCommand command = sql.CreateCommand();
                string parameter = parameterStorage.GetParameterAsString();
                //pfusch
                if (loss > 10000) //should NEVER be that high! optimal error is <1
                {
                    Console.WriteLine("Fehler viel zu hoch");
                    while (loss > 10000) //stackoverflow otherwise
                    {
                        loss = loss / 1000;
                    }
                }

                string error = loss.ToString().Replace(',', '.');

                command.CommandText = $"INSERT INTO output_data (coa_id, cat_id, value, error) VALUES ({coa_id},{cat_id},'{parameter}',{error});";
                Console.WriteLine(command.CommandText);
                await command.ExecuteNonQueryAsync();
            }
        }
        //autogeneriert sql statement
        private string ConcatSQLConditions(List<string> ids, string statement)
        {
            string temp = String.Join(" OR ", ids);
            return statement + " WHERE " + temp;
        }

        /// <summary>
        ///Returns list of names based on a list with ids of categories
        /// </summary>
        /// <param name="catids"></param>
        /// <returns></returns>
        private async Task<List<string>> GetNamesOfCategoriesByIDsAsync(List<int> catids)
        {
            using (SqlConnection sqlc = new SqlConnection(ki_read_input))
            {
                await sqlc.OpenAsync();
                SqlCommand command = sqlc.CreateCommand();
                if (catids.Count == 0)
                {
                    command.CommandText = "SELECT name FROM category ORDER BY cat_id;";
                }
                else
                {
                    List<string> statementConditions = new List<string>();
                    foreach (var item in catids)
                    {
                        statementConditions.Add("cat_id=" + item);
                    }
                    command.CommandText = ConcatSQLConditions(statementConditions, "SELECT name FROM category") + " ORDER BY cat_id;";
                }

                List<string> disziplin = new List<string>();
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) //fraglich ob es nicht eine bessere Methode gibt
                    {
                        disziplin.Add((string)reader["name"]);
                    }
                }
                return disziplin;
            }
        }
    }
}