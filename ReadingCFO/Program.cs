using AngleSharp;
using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        public static string _filter = "CLEOMAR";
        static int _page = 1;
        static int _pages = 0;
        static int _registrosProcessados = 0;
        static int _totalDeRegistros = 0;
        static bool _enabledLogs = true;
        #endregion

        public static string _arquivo;
        static List<Dentista> _listaDentistas = new List<Dentista>();

        static async Task Main(string[] args)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            //_arquivo = Directory.GetCurrentDirectory() + "\\dados.txt";

            //Se existir arquivo deletar!
            //if (System.IO.File.Exists(_arquivo))
            //    System.IO.File.Delete(_arquivo);

            //System.IO.File.Create(_arquivo).Close();
            //Console.WriteLine($"                                                                     ");

            await CalcularPaginas();

            //Quebrar em 2 Processos
            var numberOfProcess = 2;

            var pagesOfProcessOne = _pages / numberOfProcess;
            var pagesOfProcessTwo = pagesOfProcessOne + 1;


            var tarefa = StartByWebSiteCFO(1, pagesOfProcessOne);
            var tarefa2 = StartByWebSiteCFO(pagesOfProcessTwo, _pages);

            var resTaskOne = await tarefa;
            var resTaskTwo = await tarefa2;

            stopWatch.Stop();

            showEnd(stopWatch.Elapsed);
        }

        static async Task<bool> StartByWebSiteCFO(int startInPage, int endPage)
        {
            showInit(startInPage);

            var context = BrowsingContext.New(
               Configuration.Default.WithDefaultLoader()
               .WithDefaultCookies()
               );

            updatePageOfUrl();

            var dataPage = await context.OpenAsync(_url);

            var statusCode = dataPage.StatusCode;
            if (statusCode != System.Net.HttpStatusCode.OK)
                Console.WriteLine($"Servidor retornou código: {statusCode}");

            var Element = dataPage.GetElementsByClassName("entry-content").FirstOrDefault();

            var nosFiltrados = cleanGarbage(Element);

            var array = fillInArray(nosFiltrados);

            writeInList(array);

            //if (_pages >= _page)
            if (endPage > startInPage)
                await StartByWebSiteCFO(++startInPage, endPage);

            return true;
        }

        static List<INode> cleanGarbage(IElement element)
        {
            element.QuerySelector("div").Remove();
            element.QuerySelector("script").Remove();

            var Atualizacao = element.GetElementsByTagName("h6").FirstOrDefault();
            var ultimaAtualizacao = Atualizacao.InnerHtml;

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

        static void writeInList(string[,] array)
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

                var res = 1;
                //Salva na Lista
                //_listaDentistas.Add();

                //new Dentista(
                //reg[0].TextContent.Split("-")[0],
                //reg[0].TextContent.Split("-")[1].Split(":")[1],
                //reg[1].TextContent,
                //reg[2].TextContent.Split(":")[1],
                //reg[3].TextContent.Split(":")[1],
                //reg[4].TextContent.Split(":")[1].Trim(),
                //reg[5].TextContent.Split(":")[1].Trim()
                //)
            }

            awaitWebServer();

        }

        static void writeInFile(string[,] array)
        {
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

            awaitWebServer();

            //showProgress(linhas);

            //sw.Close();
        }

        static async Task CalcularPaginas()
        {
            var context = BrowsingContext.New(
               Configuration.Default.WithDefaultLoader()
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

            Console.WriteLine($"Total de Registros encontrados: {_totalDeRegistros}");

            var valorDecimal = (decimal)_totalDeRegistros / 10;
            _pages = (int)Math.Ceiling(valorDecimal);
        }

        #region Funções
        static void awaitWebServer()
        {
            if (ehMultiplo(_page))
            {
                Console.WriteLine($"Dando um descanso ao Servidor!");
                Thread.Sleep(400000); //Thread.Sleep(5000) => 5 Segundos / (300000) => 5 minutos
                Console.WriteLine($"Servidor pronto para continuar!");
            }
        }
        static bool ehMultiplo(int num) => ((num % 250) == 0);
        static void updatePageOfUrl() => _url = _urlBase + $"?num_pagina={_page}&nome={_filter}";
        #endregion

        #region Mensagens 

        static void showInit(int page)
        {
            Console.WriteLine($"                                                                     ");
            Console.WriteLine($"INICIANDO LEITURA DA PÁGINA {page}");
            Console.WriteLine($"                                                                     ");
        }

        static void showEnd(TimeSpan ts)
        {
            string elapsedTime = String.Format("{0:00} horas {1:00} minutos {2:00} segundos {3:00} milésimos",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);

            Console.WriteLine($"Finalizada Leitura em {elapsedTime}!");
        }
        static void showProgress(int linhas)
        {
            if (_page == (_pages + 1))
                _registrosProcessados = _registrosProcessados + (_totalDeRegistros - _registrosProcessados);
            else
                _registrosProcessados = _registrosProcessados + linhas;

            Console.WriteLine($"                                                                     ");
            Console.WriteLine($"{_registrosProcessados} de {_totalDeRegistros} registro importados!");
        }
        static (string, string, string) showNode(INode node)
        {
            if (_enabledLogs)
            {
                if (node.NodeName != "HR")
                {
                    //Console.WriteLine($"                                                                     ");
                    //Console.WriteLine($"|====================================================================");
                    Console.WriteLine($"|  Conteudo: {node.TextContent}");
                    //Console.WriteLine($"|  Tipo: {node.NodeType}");
                    //Console.WriteLine($"|  Valor: {node.NodeValue}");
                    //Console.WriteLine($"|  Nome: {node.NodeName}");
                    //Console.WriteLine($"|====================================================================");
                }
                if (node.NodeName == "HR")
                    Console.WriteLine($"|====================================================================");
            }

            return (node.NodeValue, node.TextContent, node.NodeName);
        }
        #endregion
    }

    #region Classes
    public class Dentista
    {
        public Dentista(
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
        }

        public string Funcao { get; set; }
        public string Inscricao { get; set; }
        public string Nome { get; set; }
        public string Situacao { get; set; }
        public string Tipo { get; set; }
        public string Especialidades { get; set; }
        public DateTime InscricaoCRO { get; set; }
        public DateTime RegistroCRO { get; set; }
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
    #endregion
}