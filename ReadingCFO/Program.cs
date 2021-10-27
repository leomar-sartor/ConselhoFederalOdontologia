using AngleSharp;
using AngleSharp.Dom;
using Dapper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ReadingCFO
{
    class Program
    {
        #region Propriedade Globais
        static string _url = "";
        static string _urlBase = "https://website.cfo.org.br/profissionais-cadastrados/";
        public static string _filter = "+";
        static int _page = 1;
        static int _pages = 0;
        static int _registrosProcessados = 0;
        static int _totalDeRegistros = 0;
        static bool _enabledLogs = false;
        static int _tempo = 400000;
        public static string database = "Data source=DbServerDEV; Initial Catalog=CrawlerCFO; User Id=appdotnet; Password=appdotnetdev;Connect Timeout=30";

        static List<Dentista> _listaDentistasUm = new List<Dentista>();
        static List<Dentista> _listaDentistasDois = new List<Dentista>();
        static List<Dentista> _listaDentistasTres = new List<Dentista>();
        static List<Dentista> _listaDentistasQuatro = new List<Dentista>();
        static List<Dentista> _listaDentistasCinco = new List<Dentista>();
        #endregion

        static async Task Main(string[] args)
        {
            var conexaoBanco = ConfigurationSettings.AppSettings["ConnectionString"];
            if (!String.IsNullOrWhiteSpace(conexaoBanco))
                database = conexaoBanco;

            var buscar = ConfigurationSettings.AppSettings["Name"];
            if (!String.IsNullOrWhiteSpace(buscar))
                _filter = buscar;

            var tempo = ConfigurationSettings.AppSettings["Tempo"];
            if (!String.IsNullOrWhiteSpace(tempo))
                _tempo = Int32.Parse(tempo);

            var log = ConfigurationSettings.AppSettings["Log"];
            if (!String.IsNullOrWhiteSpace(log))
                _enabledLogs = Boolean.Parse(log);

            showInit();

            Stopwatch stopWatch = new Stopwatch();

            try
            {
                stopWatch.Start();

                await CalcularPaginas();

                var numberOfProcess = 5;

                int paginasPorTarefa = 1;
                if (_pages >= numberOfProcess)
                    paginasPorTarefa = _pages / numberOfProcess;
                else
                {
                    var dec = (decimal)_totalDeRegistros / 10;
                    paginasPorTarefa = (int)Math.Ceiling(dec);
                }

                int inicio = 1;
                int final = paginasPorTarefa * 1;

                //Buscar ultimo registro no banco e setar como valor inicial
                var processos = await BuscarUltimoProcessamento();

                var paginaProcessoUm = processos.Where(m => m.Processo == "1").FirstOrDefault();
                if (paginaProcessoUm != null)
                    inicio = int.Parse(paginaProcessoUm.UltimaPaginaProcessada) + 1;

                var tarefaUm = StartByWebSiteCFO(inicio, final, (final <= _pages), 1);

                inicio = final + 1;
                final = inicio + paginasPorTarefa;

                var paginaProcessoDois = processos.Where(m => m.Processo == "2").FirstOrDefault();
                if (paginaProcessoDois != null)
                    inicio = int.Parse(paginaProcessoDois.UltimaPaginaProcessada) + 1;

                var tarefaDois = StartByWebSiteCFO(inicio, final, (final <= _pages), 2);

                inicio = final + 1;
                final = inicio + paginasPorTarefa;

                var paginaProcessoTres = processos.Where(m => m.Processo == "3").FirstOrDefault();
                if (paginaProcessoTres != null)
                    inicio = int.Parse(paginaProcessoTres.UltimaPaginaProcessada) + 1;

                var tarefaTres = StartByWebSiteCFO(inicio, final, (final <= _pages), 3);

                inicio = final + 1;
                final = inicio + paginasPorTarefa;

                var paginaProcessoQuatro = processos.Where(m => m.Processo == "4").FirstOrDefault();
                if (paginaProcessoQuatro != null)
                    inicio = int.Parse(paginaProcessoQuatro.UltimaPaginaProcessada) + 1;

                var tarefaQuatro = StartByWebSiteCFO(inicio, final, (final <= _pages), 4);

                inicio = final + 1;
                final = (_pages - inicio) + inicio;

                var paginaProcessoCinco = processos.Where(m => m.Processo == "5").FirstOrDefault();
                if (paginaProcessoCinco != null)
                    inicio = int.Parse(paginaProcessoCinco.UltimaPaginaProcessada) + 1;

                var tarefaCinco = StartByWebSiteCFO(inicio, final, (final <= _pages && inicio < _pages), 5);

                //Gravar Log de Processamento
                using (IDbConnection db = new SqlConnection(database))
                {
                    var sql = @"INSERT IMPORTAR (Data, TotalDeRegistros, TotalDePaginas)
                                OUTPUT INSERTED.* VALUES (@Data, @TotalDeRegistros, @TotalDePaginas)";

                    var retorno = await db.QuerySingleAsync<Importar>(sql, new
                    {
                        Data = DateTime.Now,
                        TotalDeRegistros = _totalDeRegistros,
                        TotalDePaginas = _pages
                    });
                }

                var resTaskOne = await tarefaUm;
                var resTaskTwo = await tarefaDois;
                var resTaskThree = await tarefaTres;
                var resTaskFour = await tarefaQuatro;
                var resTaskFive = await tarefaCinco;

                await Terminar();

                stopWatch.Stop();

                showEnd(stopWatch.Elapsed);

            }
            catch (Exception e)
            {
                Console.WriteLine($"##-- ¯\\_(ツ)_/¯ | (╯°□°)╯ pqp --##");

                Console.WriteLine($"Erro: Mensagem: {e.Message}!");
                Console.WriteLine($"Erro: Inner Exception: {e.InnerException}!");
                Console.WriteLine($"Erro: Stack Trace: {e.StackTrace}!");

                await Terminar();

                stopWatch.Stop();

                var ts = stopWatch.Elapsed;
                string elapsedTime = String.Format("{0:00} horas {1:00} minutos {2:00} segundos {3:00} milésimos",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);

                Console.WriteLine($"                                                                     ");
                Console.WriteLine($"Finalizada por Excessão! Leitura em {elapsedTime}!");
            }
        }

        static async Task<bool> StartByWebSiteCFO(int startInPage, int endPage, bool fazer, int numeroProcesso)
        {
            if (fazer)
            {
                Console.WriteLine($"{numeroProcesso}: INICIANDO LEITURA DA PÁGINA {startInPage}");

                var context = BrowsingContext.New(
                   AngleSharp.Configuration.Default.WithDefaultLoader()
                   .WithDefaultCookies()
                   );

                updatePageOfUrl(startInPage);

                var dataPage = await context.OpenAsync(_url);

                var statusCode = dataPage.StatusCode;
                if (statusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($"|________________________________- {statusCode} - __________________________________|");
                    Console.WriteLine($"|----- Processo: {numeroProcesso} | Page: {startInPage} aguardando um momento! -----|");
                    Console.WriteLine($"|___________________________________________________________________________________|");
                    Thread.Sleep(_tempo);
                    await StartByWebSiteCFO(startInPage, endPage, true, numeroProcesso);
                    Console.WriteLine($"---------------------------");
                }

                var Element = dataPage.GetElementsByClassName("entry-content").FirstOrDefault();

                if (Element != null)
                {
                    var nosFiltrados = cleanGarbage(Element);

                    var array = fillInArray(nosFiltrados);

                    await writeInList(array, numeroProcesso, startInPage);

                    if (endPage > startInPage)
                        await StartByWebSiteCFO(++startInPage, endPage, true, numeroProcesso);

                    return true;
                }
                else
                {
                    Console.WriteLine($"|___________________________________________________________________________________|");
                    Console.WriteLine($"|----- Processo: {numeroProcesso} | Page: {startInPage} aguardando um momento! -----|");
                    Console.WriteLine($"|___________________________________________________________________________________|");
                    Thread.Sleep(_tempo);
                    await StartByWebSiteCFO(startInPage, endPage, true, numeroProcesso);
                }
            }

            return false;
        }

        #region Funções
        static async Task Terminar()
        {
            var listaUm = new List<Dentista>();
            listaUm.AddRange(_listaDentistasUm);
            _listaDentistasUm.Clear();
            await GravarListaUm(listaUm);
            listaUm.Clear();

            var listaDois = new List<Dentista>();
            listaDois.AddRange(_listaDentistasDois);
            _listaDentistasDois.Clear();
            await GravarListaDois(listaDois);
            listaDois.Clear();

            var listaTres = new List<Dentista>();
            listaTres.AddRange(_listaDentistasTres);
            _listaDentistasTres.Clear();
            await GravarListaTres(listaTres);
            listaTres.Clear();

            var listaQuatro = new List<Dentista>();
            listaQuatro.AddRange(_listaDentistasQuatro);
            _listaDentistasQuatro.Clear();
            await GravarListaQuatro(listaQuatro);
            listaQuatro.Clear();

            var listaCinco = new List<Dentista>();
            listaCinco.AddRange(_listaDentistasCinco);
            _listaDentistasCinco.Clear();
            await GravarListaCinco(listaCinco);
            listaCinco.Clear();
        }

        static List<INode> cleanGarbage(IElement element)
        {
            element.QuerySelector("div").Remove();
            element.QuerySelector("script").Remove();

            var Atualizacao = element.GetElementsByTagName("h6").FirstOrDefault();
            if (Atualizacao != null)
                element.QuerySelector("h6").Remove();

            // Eliminar Nós sem Valor e Deixar HR - Usa o <HR> como separador
            var nos = element.ChildNodes
                .Where(n =>
                    (n.NodeName == "#text" && !String.IsNullOrWhiteSpace(n.TextContent))
                        || n.NodeName == "HR" || n.NodeName == "B")
                .ToList();

            //Remover primeiro separador
            nos.RemoveAt(0);
            nos.RemoveAt(0);
            //Remover os nós da Paginação
            nos.RemoveAt(nos.Count - 1);

            if (!nos[nos.Count - 1].Text().Contains("Data"))
                nos.RemoveAt(nos.Count - 1);

            return nos;
        }

        /// <summary>
        /// Linhas e Colunas (10 registros por página e 7 colunas de informação)
        /// </summary>
        /// <param name="nos"></param>
        static string[,] fillInArray(List<INode> nos)
        {
            int registro = 0;

            var arrayNodes = new string[10, 7];

            foreach (var no in nos)
            {
                var (valor, conteudo, nome) = showNode(no);

                if (nome == "HR") //Separador de CFO
                {
                    registro += 1;
                    continue;
                }

                if (nome == "B") //Nome
                {
                    arrayNodes[registro, 1] = conteudo;
                    continue;
                }

                if (valor.Contains("- Insc")) //Cargo ou Inscrição
                {
                    arrayNodes[registro, 0] = valor;
                    continue;
                }

                if (valor.Contains("Sit")) //Situação
                {
                    arrayNodes[registro, 2] = valor;
                    continue;
                }

                if (valor.Contains("Tip")) //Tipo de Inscrição
                {
                    arrayNodes[registro, 3] = valor;
                    continue;
                }

                if (valor.Contains("Espec")) //Especialidade
                {
                    arrayNodes[registro, 4] = valor;
                    continue;
                }

                if (valor.Contains("Data de inscri")) //Data de Inscrição
                {
                    arrayNodes[registro, 5] = valor;
                    continue;
                }

                if (valor.Contains("Data de regis")) //Data de Registro
                {
                    arrayNodes[registro, 6] = valor;
                    continue;
                }
            }

            _page = _page + 1;

            return arrayNodes;
        }

        static async Task writeInList(string[,] array, int process, int page)
        {

            var linhas = array.GetLength(0);
            for (int i = 0; i < linhas; i++)
            {
                var line = "";
                var colunas = array.GetLength(1);

                for (int j = 0; j < colunas; j++)
                {
                    if (!String.IsNullOrEmpty(array[i, j]))
                        line = line + array[i, j].Trim() + ";";
                }

                var dados = line.Split(";").Where(val => !String.IsNullOrWhiteSpace(val)).ToArray();

                Dentista dentista;

                if (dados.Length == 7)
                {
                    var lista = dados[0].Split(" - ");

                    string funcao;
                    string insc;
                    if (lista.Length == 2)
                    {
                        funcao = dados[0].Split("-")[0].Trim();
                        insc = dados[0].Split(" - ")[1].Split(":")[1].Trim();
                    }
                    else
                    {
                        funcao = "";
                        insc = dados[0].Split(":")[1].Trim();
                    }

                    string dataRegistro;
                    dataRegistro = dados[6].Split(":")[1].Trim();
                    if (String.IsNullOrWhiteSpace(dados[6].Split(":")[1]))
                    {
                        dataRegistro = DateTime.Today.ToString().Split(" ")[0];
                    }

                    string dataIncricao;
                    dataIncricao = dados[6].Split(":")[1].Trim();
                    if (String.IsNullOrWhiteSpace(dados[6].Split(":")[1]))
                    {
                        dataIncricao = DateTime.Today.ToString().Split(" ")[0];
                    }

                    dentista = new Dentista(
                        process.ToString(),
                        Int64.Parse(page.ToString()),
                        funcao,
                        insc,
                        dados[1],
                        dados[2].Split(":")[1].Trim(),
                        dados[3].Split(":")[1].Trim(),
                        dados[4].Split(":")[1].Trim(),
                        dataIncricao,
                        dataRegistro
                        );

                    if (process == 1)
                    {
                        _listaDentistasUm.Add(dentista);
                    }
                    else if (process == 2)
                    {
                        _listaDentistasDois.Add(dentista);
                    }
                    else if (process == 3)
                    {
                        _listaDentistasTres.Add(dentista);
                    }
                    else if (process == 4)
                    {
                        _listaDentistasQuatro.Add(dentista);
                    }
                    else if (process == 5)
                    {
                        _listaDentistasCinco.Add(dentista);
                    }

                    ++_registrosProcessados;
                }

                if (dados.Length == 6)
                {
                    var lista = dados[0].Split(" - ");

                    string funcao;
                    string insc;
                    if (lista.Length == 2)
                    {
                        funcao = dados[0].Split("-")[0];
                        insc = dados[0].Split(" - ")[1].Split(":")[1].Trim();
                    }
                    else
                    {
                        funcao = "";
                        insc = dados[0].Split(":")[1].Trim();
                    }

                    string dataRegistro;
                    dataRegistro = dados[5].Split(":")[1].Trim();
                    if (String.IsNullOrWhiteSpace(dados[5].Split(":")[1]))
                    {
                        dataRegistro = DateTime.Today.ToString().Split(" ")[0];
                    }

                    string dataIncricao;
                    dataIncricao = dados[5].Split(":")[1].Trim();
                    if (String.IsNullOrWhiteSpace(dados[5].Split(":")[1]))
                    {
                        dataIncricao = DateTime.Today.ToString().Split(" ")[0];
                    }

                    dentista = new Dentista(
                        process.ToString(),
                        Int64.Parse(page.ToString()),
                        funcao.Trim(),
                        insc,
                        dados[1],
                        dados[2].Split(":")[1].Trim(),
                        dados[3].Split(":")[1].Trim(),
                        String.Empty,
                        dataIncricao,
                        dataRegistro
                        );

                    if (process == 1)
                    {
                        _listaDentistasUm.Add(dentista);
                    }
                    else if (process == 2)
                    {
                        _listaDentistasDois.Add(dentista);
                    }
                    else if (process == 3)
                    {
                        _listaDentistasTres.Add(dentista);
                    }
                    else if (process == 4)
                    {
                        _listaDentistasQuatro.Add(dentista);
                    }
                    else if (process == 5)
                    {
                        _listaDentistasCinco.Add(dentista);
                    }

                    ++_registrosProcessados;
                }
            }

            await awaitWebServer();
        }

        static async Task CalcularPaginas()
        {
            var context = BrowsingContext.New(
               AngleSharp.Configuration.Default.WithDefaultLoader()
               .WithDefaultCookies()
               );

            var url = _urlBase + $"?num_pagina={_page}&nome={_filter}";

            var dataPage = await context.OpenAsync(url);

            var Results = dataPage.GetElementsByClassName("entry-content").FirstOrDefault();

            Results.QuerySelector("div").Remove();
            Results.QuerySelector("script").Remove();

            var Atualizacao = Results.GetElementsByTagName("h6").FirstOrDefault();
            var ultimaAtualizacaoWS = Atualizacao.InnerHtml;

            Console.WriteLine($"{ultimaAtualizacaoWS}");

            Results.QuerySelector("h6").Remove();

            // Eliminar Nós sem Valor e Deixar HR - Usa o <HR> como separador
            var nosComValor = Results.ChildNodes
                .Where(n =>
                    (n.NodeName == "#text" && !String.IsNullOrWhiteSpace(n.TextContent))
                        || n.NodeName == "HR" || n.NodeName == "B")
                .ToList();

            var totalPagina = nosComValor[0].TextContent;
            _totalDeRegistros = Int32.Parse(String.Join("", Regex.Split(totalPagina, @"[^\d]")));

            Console.WriteLine($"                                                   ");
            Console.WriteLine($"Total de Registros encontrados: {_totalDeRegistros}");
            Console.WriteLine($"                                                   ");

            var valorDecimal = (decimal)_totalDeRegistros / 10;
            _pages = (int)Math.Ceiling(valorDecimal);
        }
        static async Task<IEnumerable<ProcessoViewModel>> BuscarUltimoProcessamento()
        {
            using (IDbConnection db = new SqlConnection(database))
            {
                var sql = @"SELECT 
	                            PROCESSO,
	                            MIN(PAGINA) PAGINAINICIAL,
	                            MAX(PAGINA) ULTIMAPAGINAPROCESSADA
                            FROM 
	                            CADASTROCFODENTISTA
                            GROUP BY PROCESSO";

                var processos = await db.QueryAsync<ProcessoViewModel>(sql);
                return processos;
            }
        }

        static async Task GravarListaUm(List<Dentista> dados)
        {
            using (IDbConnection db = new SqlConnection(database))
            {
                var sql = @"INSERT CADASTROCFODENTISTA (Processo, Pagina, Nome, Funcao, Inscricao, Estado, Tipo, Situacao, Especialidades, DataInscricao, DataRegistro, RegistroImportadoEm)
                                OUTPUT INSERTED.* VALUES (@Processo, @Pagina, @Nome, @Funcao, @Inscricao, @Estado, @Tipo, @Situacao, @Especialidades, @DataInscricao, @DataRegistro, @RegistroImportadoEm)";

                foreach (var dentista in dados)
                {
                    var retorno = await db.QuerySingleAsync<Dentista>(sql, new
                    {
                        dentista.Processo,
                        dentista.Pagina,
                        dentista.Nome,
                        dentista.Funcao,
                        dentista.Inscricao,
                        dentista.Estado,
                        dentista.Tipo,
                        dentista.Situacao,
                        dentista.Especialidades,
                        DataInscricao = dentista.InscricaoCRO,
                        DataRegistro = dentista.RegistroCRO,
                        dentista.RegistroImportadoEm
                    });
                }
            }
        }

        static async Task GravarListaDois(List<Dentista> dados)
        {
            using (IDbConnection db = new SqlConnection(database))
            {
                var sql = @"INSERT CADASTROCFODENTISTA (Processo, Pagina, Nome, Funcao, Inscricao, Estado, Tipo, Situacao, Especialidades, DataInscricao, DataRegistro, RegistroImportadoEm)
                                OUTPUT INSERTED.* VALUES (@Processo, @Pagina, @Nome, @Funcao, @Inscricao, @Estado, @Tipo, @Situacao, @Especialidades, @DataInscricao, @DataRegistro, @RegistroImportadoEm)";

                foreach (var dentista in dados)
                {
                    var retorno = await db.QuerySingleAsync<Dentista>(sql, new
                    {
                        dentista.Processo,
                        dentista.Pagina,
                        dentista.Nome,
                        dentista.Funcao,
                        dentista.Inscricao,
                        dentista.Estado,
                        dentista.Tipo,
                        dentista.Situacao,
                        dentista.Especialidades,
                        DataInscricao = dentista.InscricaoCRO,
                        DataRegistro = dentista.RegistroCRO,
                        dentista.RegistroImportadoEm
                    });
                }
            }
        }

        static async Task GravarListaTres(List<Dentista> dados)
        {
            using (IDbConnection db = new SqlConnection(database))
            {
                var sql = @"INSERT CADASTROCFODENTISTA (Processo, Pagina, Nome, Funcao, Inscricao, Estado, Tipo, Situacao, Especialidades, DataInscricao, DataRegistro, RegistroImportadoEm)
                                OUTPUT INSERTED.* VALUES (@Processo, @Pagina, @Nome, @Funcao, @Inscricao, @Estado, @Tipo, @Situacao, @Especialidades, @DataInscricao, @DataRegistro, @RegistroImportadoEm)";

                foreach (var dentista in dados)
                {
                    var retorno = await db.QuerySingleAsync<Dentista>(sql, new
                    {
                        dentista.Processo,
                        dentista.Pagina,
                        dentista.Nome,
                        dentista.Funcao,
                        dentista.Inscricao,
                        dentista.Estado,
                        dentista.Tipo,
                        dentista.Situacao,
                        dentista.Especialidades,
                        DataInscricao = dentista.InscricaoCRO,
                        DataRegistro = dentista.RegistroCRO,
                        dentista.RegistroImportadoEm
                    });
                }
            }
        }

        static async Task GravarListaQuatro(List<Dentista> dados)
        {
            using (IDbConnection db = new SqlConnection(database))
            {
                var sql = @"INSERT CADASTROCFODENTISTA (Processo, Pagina, Nome, Funcao, Inscricao, Estado, Tipo, Situacao, Especialidades, DataInscricao, DataRegistro, RegistroImportadoEm)
                                OUTPUT INSERTED.* VALUES (@Processo, @Pagina, @Nome, @Funcao, @Inscricao, @Estado, @Tipo, @Situacao, @Especialidades, @DataInscricao, @DataRegistro, @RegistroImportadoEm)";

                foreach (var dentista in dados)
                {
                    var retorno = await db.QuerySingleAsync<Dentista>(sql, new
                    {
                        dentista.Processo,
                        dentista.Pagina,
                        dentista.Nome,
                        dentista.Funcao,
                        dentista.Inscricao,
                        dentista.Estado,
                        dentista.Tipo,
                        dentista.Situacao,
                        dentista.Especialidades,
                        DataInscricao = dentista.InscricaoCRO,
                        DataRegistro = dentista.RegistroCRO,
                        dentista.RegistroImportadoEm
                    });
                }
            }
        }

        static async Task GravarListaCinco(List<Dentista> dados)
        {
            using (IDbConnection db = new SqlConnection(database))
            {
                var sql = @"INSERT CADASTROCFODENTISTA (Processo, Pagina, Nome, Funcao, Inscricao, Estado, Tipo, Situacao, Especialidades, DataInscricao, DataRegistro, RegistroImportadoEm)
                                OUTPUT INSERTED.* VALUES (@Processo, @Pagina, @Nome, @Funcao, @Inscricao, @Estado, @Tipo, @Situacao, @Especialidades, @DataInscricao, @DataRegistro, @RegistroImportadoEm)";

                foreach (var dentista in dados)
                {
                    var retorno = await db.QuerySingleAsync<Dentista>(sql, new
                    {
                        dentista.Processo,
                        dentista.Pagina,
                        dentista.Nome,
                        dentista.Funcao,
                        dentista.Inscricao,
                        dentista.Estado,
                        dentista.Tipo,
                        dentista.Situacao,
                        dentista.Especialidades,
                        DataInscricao = dentista.InscricaoCRO,
                        DataRegistro = dentista.RegistroCRO,
                        dentista.RegistroImportadoEm
                    });
                }
            }
        }

        static async Task awaitWebServer(bool terminar = false)
        {
            bool pular = false;

            if (terminar)
                pular = true;

            if (!pular)
                if (ehMultiplo(_listaDentistasUm.Count()) || terminar)
                {
                    var lista = new List<Dentista>();
                    lista.AddRange(_listaDentistasUm);
                    _listaDentistasUm.Clear();

                    await GravarListaUm(lista);
                    lista.Clear();
                    Console.WriteLine($"                                    ");
                    Console.WriteLine($"======= Processo 1 relaxando =======");
                    Console.WriteLine($"                                    ");
                    Thread.Sleep(_tempo);
                }

            if (!pular)
                if (ehMultiplo(_listaDentistasDois.Count()) || terminar)
                {
                    var lista = new List<Dentista>();
                    lista.AddRange(_listaDentistasDois);
                    _listaDentistasDois.Clear();

                    await GravarListaDois(lista);
                    lista.Clear();
                    Console.WriteLine($"                                    ");
                    Console.WriteLine($"======= Processo 2 relaxando =======");
                    Console.WriteLine($"                                    ");
                    Thread.Sleep(_tempo);
                }

            if (!pular)
                if (ehMultiplo(_listaDentistasTres.Count()) || terminar)
                {
                    var lista = new List<Dentista>();
                    lista.AddRange(_listaDentistasTres);
                    _listaDentistasTres.Clear();

                    await GravarListaTres(lista);
                    lista.Clear();
                    Console.WriteLine($"                                    ");
                    Console.WriteLine($"======= Processo 3 relaxando =======");
                    Console.WriteLine($"                                    ");
                    Thread.Sleep(_tempo);
                }

            if (!pular)
                if (ehMultiplo(_listaDentistasQuatro.Count()) || terminar)
                {
                    var lista = new List<Dentista>();
                    lista.AddRange(_listaDentistasQuatro);
                    _listaDentistasQuatro.Clear();

                    await GravarListaQuatro(lista);
                    lista.Clear();
                    Console.WriteLine($"                                    ");
                    Console.WriteLine($"======= Processo 4 relaxando =======");
                    Console.WriteLine($"                                    ");
                    Thread.Sleep(_tempo);
                }

            if (!pular)
                if (ehMultiplo(_listaDentistasCinco.Count()) || terminar)
                {
                    var lista = new List<Dentista>();
                    lista.AddRange(_listaDentistasCinco);
                    _listaDentistasCinco.Clear();

                    await GravarListaCinco(lista);
                    lista.Clear();
                    Console.WriteLine($"                                    ");
                    Console.WriteLine($"======= Processo 5 relaxando =======");
                    Console.WriteLine($"                                    ");
                    Thread.Sleep(_tempo);
                }

            if (pular && terminar)
                return;

            if (_registrosProcessados == _totalDeRegistros)
            {
                await awaitWebServer(true);

                Thread.Sleep(_tempo);

                await Terminar();

                return;
            }
        }

        static bool ehMultiplo(int num) => ((num % 250) == 0) && num != 0;
        static void updatePageOfUrl(int page) => _url = _urlBase + $"?num_pagina={page}&nome={_filter}";
        #endregion

        #region Mensagens 

        static void showInit()
        {
            Console.WriteLine(@$"
    :::::::::  :::   ::: 
    :+:    :+: :+:   :+:    
    +:+    +:+  +:+ +:+       
    +#++:++#+    +#++:   
    +#+    +#+    +#+     
    #+#    #+#    #+#    
    #########     ###    ");

            Console.WriteLine(@"
        :::        :::::::::: ::::::::  ::::    ::::      :::     :::::::::  
        :+:        :+:       :+:    :+: +:+:+: :+:+:+   :+: :+:   :+:    :+: 
        +:+        +:+       +:+    +:+ +:+ +:+:+ +:+  +:+   +:+  +:+    +:+ 
        +#+        +#++:++#  +#+    +:+ +#+  +:+  +#+ +#++:++#++: +#++:++#:  
        +#+        +#+       +#+    +#+ +#+       +#+ +#+     +#+ +#+    +#+ 
        #+#        #+#       #+#    #+# #+#       #+# #+#     #+# #+#    #+# 
        ########## ########## ########  ###       ### ###     ### ###    ### 

                 ::::::::      :::     ::::::::: ::::::::::: ::::::::  :::::::::     
                :+:    :+:   :+: :+:   :+:    :+:    :+:    :+:    :+: :+:    :+:    
                +:+         +:+   +:+  +:+    +:+    +:+    +:+    +:+ +:+    +:+    
                +#++:++#++ +#++:++#++: +#++:++#:     +#+    +#+    +:+ +#++:++#:     
                       +#+ +#+     +#+ +#+    +#+    +#+    +#+    +#+ +#+    +#+    
                #+#    #+# #+#     #+# #+#    #+#    #+#    #+#    #+# #+#    #+#    
                 ########  ###     ### ###    ###    ###     ########  ###    ###    ");

            Console.WriteLine($"                                                                     ");

            Console.WriteLine($"Connection: {database}");
            Console.WriteLine($"Filtro: {_filter}");
            Console.WriteLine($"                                                                     ");
        }

        static void showEnd(TimeSpan ts)
        {
            string elapsedTime = String.Format("{0:00} horas {1:00} minutos {2:00} segundos {3:00} milésimos",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);

            Console.WriteLine($"                                                                     ");
            Console.WriteLine($"Finalizada Leitura em {elapsedTime}!");
        }

        static (string, string, string) showNode(INode node)
        {
            if (_enabledLogs)
            {
                if (node.NodeName != "HR")
                {
                    Console.WriteLine($"                                                                     ");
                    Console.WriteLine($"|====================================================================");
                    Console.WriteLine($"|  Conteudo: {node.TextContent}");
                    Console.WriteLine($"|  Tipo: {node.NodeType}");
                    Console.WriteLine($"|  Valor: {node.NodeValue}");
                    Console.WriteLine($"|  Nome: {node.NodeName}");
                    Console.WriteLine($"|====================================================================");
                    Console.WriteLine($"                                                                     ");
                }
                if (node.NodeName == "HR")
                {
                    Console.WriteLine($"                                                                     ");
                    Console.WriteLine($"|====================================================================");
                    Console.WriteLine($"                                                                     ");
                }
            }

            return (node.NodeValue, node.TextContent, node.NodeName);
        }
        #endregion
    }

    #region Classes

    public class ProcessoViewModel
    {
        public string Processo { get; set; }
        public string PaginaInicial { get; set; }
        public string UltimaPaginaProcessada { get; set; }
    }

    public class Importar
    {
        public DateTime Data { get; set; }
        public long TotalDeRegistros { get; set; }
        public long TotalDePaginas { get; set; }
    }
    public class Dentista
    {
        public Dentista()
        {

        }
        public Dentista(
            string processo,
            long page,
            string funcao,
            string inscricao,
            string nome,
            string situacao,
            string tipo,
            string especialidades,
            string inscricaoCRO,
            string registroCRO
            )
        {
            Processo = processo;
            Pagina = page;
            Funcao = funcao;
            Inscricao = inscricao;
            Nome = nome;
            Situacao = situacao;
            Tipo = tipo;
            Especialidades = especialidades;

            var diaInsc = Int32.Parse(inscricaoCRO.Split("/")[0]);
            var mesInsc = Int32.Parse(inscricaoCRO.Split("/")[1]);
            var anoInsc = Int32.Parse(inscricaoCRO.Split("/")[2]);
            InscricaoCRO = new DateTime(anoInsc, mesInsc, diaInsc);

            var diaReg = Int32.Parse(registroCRO.Split("/")[0]);
            var mesReg = Int32.Parse(registroCRO.Split("/")[1]);
            var anoReg = Int32.Parse(registroCRO.Split("/")[2]);
            RegistroCRO = new DateTime(anoReg, mesReg, diaReg);

            Estado = Inscricao.Split("-")[0];
            RegistroImportadoEm = DateTime.Now;
        }

        public string Processo { get; set; }
        public long Pagina { get; set; }
        public string Funcao { get; set; }
        public string Inscricao { get; set; }
        public string Nome { get; set; }
        public string Situacao { get; set; }
        public string Tipo { get; set; }
        public string Especialidades { get; set; }
        public string Estado { get; set; }
        public DateTime InscricaoCRO { get; set; }
        public DateTime RegistroCRO { get; set; }
        public DateTime RegistroImportadoEm { get; set; }

    }
    #endregion

    #region Exemplo com Form's e Paralelismo
    //var context = BrowsingContext.New(
    //   Configuration.Default.WithDefaultLoader()
    //   .WithDefaultCookies()
    //   );

    //var Page = await context.OpenAsync(_url);
    //var form = Page.Forms["formConsulta"];

    //var fieldEnrollment = form["inscricao"] as IHtmlInputElement;

    //var fieldName = form["nome"] as IHtmlInputElement;
    //fieldName.Value = " ";

    //var resultado = await form.SubmitAsync();
    //var Results = resultado.GetElementsByClassName("entry-content").FirstOrDefault();
    //Results.QuerySelector("div").Remove();
    //Results.QuerySelector("script").Remove();

    //var Atualizacao = Results.GetElementsByTagName("h6").FirstOrDefault();
    //var ultimaAtualizacao = Atualizacao.InnerHtml;

    //Results.QuerySelector("h6").Remove();

    ////Eliminar Nós sem Valor e Deixar HR - Usa o <HR> como separador 
    //var nosComValor = Results.ChildNodes
    //    .Where(n => !String.IsNullOrWhiteSpace(n.TextContent) && n.NodeName != "HR" && n.NodeName != "A")
    //    .ToList();

    //var totailPagina = nosComValor[0].TextContent;
    //var total = Int32.Parse(String.Join("", Regex.Split(totailPagina, @"[^\d]")));
    //Console.WriteLine($"Total de Registros encontrados: {total}");

    ////var totalDePaginas = total / 10;

    //nosComValor.RemoveAt(0);
    ////Remover a Paginação
    //nosComValor.RemoveAt(nosComValor.Count - 1);
    //nosComValor.RemoveAt(nosComValor.Count - 1);

    //var quebrados = nosComValor.Select((x, i) => new { Index = i, Value = x })
    //        .GroupBy(x => x.Index / 6)
    //        .Select(x => x.Select(v => v.Value).ToList())
    //        .ToList();

    //var lista = new List<Medico>();

    //Parallel.ForEach(quebrados, new ParallelOptions { MaxDegreeOfParallelism = 10 }, reg =>
    //{
    //    lista.Add(new Medico(
    //        reg[0].TextContent.Split("-")[0],
    //        reg[0].TextContent.Split("-")[1].Split(":")[1],
    //        reg[1].TextContent,
    //        reg[2].TextContent.Split(":")[1],
    //        reg[3].TextContent.Split(":")[1],
    //        reg[4].TextContent.Split(":")[1].Trim(),
    //        reg[5].TextContent.Split(":")[1].Trim()
    //        ));
    //});



    //Conexão:[DbServerDEV]
    //Banco:[CrawlerCFO]
    //Tabela:[CadastroCfoDentista]

    //Id (cod_crmo)
    //Nome (nome_medi)
    //Funcao (Não tem no Layout)
    //Inscricao (Não tem no Layout)
    //Estado (est_medi)
    //Tipo (Status_1): Principal / Secundaria
    //Situação (Status_2) Ativo / Cancelado
    //Especialidades (Especialidade)
    //DataInscricao (Não tem no Layout)
    //DataRegistro (Não tem no Layout)
    //RegistroImportadoEm (DataCriacao)

    //public static string database = "Data source=FUNCN3364\\SQLEXPRESS;Database=Crawler;Trusted_Connection=True;MultipleActiveResultSets=true";
    //"data source=DESKTOP-SARTOR\\SQLEXPRESS;Database=Carteira;Trusted_Connection=True;MultipleActiveResultSets=true"

    //_arquivo = Directory.GetCurrentDirectory() + "\\dados.txt";

    //Se existir arquivo deletar!
    //if (System.IO.File.Exists(_arquivo))
    //    System.IO.File.Delete(_arquivo);

    //System.IO.File.Create(_arquivo).Close();
    //Console.WriteLine($"                                                                     ");

    //static void writeInFile(string[,] array)
    //{
    //StreamWriter sw = new StreamWriter(_arquivo, true);

    //var linhas = array.GetLength(0);
    //for (int i = 0; i < linhas; i++)
    //{
    //    var line = "";
    //    var colunas = array.GetLength(1);

    //    for (int j = 0; j < colunas; j++)
    //    {
    //        if (!String.IsNullOrEmpty(array[i, j]))
    //            line = line + array[i, j].Trim() + ";";
    //    }

    //    sw.WriteLine(line);
    //}

    //awaitWebServer();

    //showProgress(linhas);

    //sw.Close();
    //}

    //static void awaitWebServer()
    //{
    //    if (ehMultiplo(_page))
    //    {
    //        Console.WriteLine($"Dando um descanso ao Servidor!");
    //        Thread.Sleep(400000); //Thread.Sleep(5000) => 5 Segundos / (300000) => 5 minutos
    //        Console.WriteLine($"Servidor pronto para continuar!");
    //    }
    //}
    #endregion
}