using AngleSharp;
using System;
using System.Threading.Tasks;
using System.Linq;
using AngleSharp.Html.Dom;
using AngleSharp.Dom;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading;
using System.Diagnostics;

namespace ReadingCFO
{
    class Program
    {
        //Utilizado com o objetivo de gravar no banco as informações. Desta forma se acontece algum erro,
        //voltar ao processo de onde tinha parado!
        static string _connection = "";
        //Url que contém parametro de paginação (num_pagina=1) e de busca(nome=+), onde + corresponde a espaço
        static string _urlBase = "https://website.cfo.org.br/profissionais-cadastrados/";
        static string _url = "";
        static int _page = 1;
        static int _pages = 0;

        static List<Dentista> _listaDentistas = new List<Dentista>();
        static string[,] _arrayNodes;

        static bool _enabledLogs = true;

        static async Task StartByWebSiteCFO()
        {
            Console.WriteLine($"Inciando leitura da página {_page}");

            var context = BrowsingContext.New(
               Configuration.Default.WithDefaultLoader()
               .WithDefaultCookies()
               );

            await updatePageOfUrl();

            var dataPage = await context.OpenAsync(_url);

            var Results = dataPage.GetElementsByClassName("entry-content").FirstOrDefault();

            Results.QuerySelector("div").Remove();
            Results.QuerySelector("script").Remove();

            var Atualizacao = Results.GetElementsByTagName("h6").FirstOrDefault();
            var ultimaAtualizacao = Atualizacao.InnerHtml;

            Results.QuerySelector("h6").Remove();

            // Eliminar Nós sem Valor e Deixar HR - Usa o <HR> como separador
            var nosComValor = Results.ChildNodes
                .Where(n => 
                    (n.NodeName == "#text" && !String.IsNullOrWhiteSpace(n.TextContent))
                        || n.NodeName == "HR" || n.NodeName == "B")
                .ToList();

            var totailPagina = nosComValor[0].TextContent;
            var _numeroDeRegistros = Int32.Parse(String.Join("", Regex.Split(totailPagina, @"[^\d]")));
            Console.WriteLine($"Total de Registros encontrados: {_numeroDeRegistros}");
            var valorDecimal = (decimal)_numeroDeRegistros / 10;
            _pages = (int)Math.Ceiling(valorDecimal);
            //Remover nó de Total de Páginas
            nosComValor.RemoveAt(0);
            //Remover primeiro separador
            nosComValor.RemoveAt(0);
            ////Remover os nós da Paginação
            nosComValor.RemoveAt(nosComValor.Count - 1);
            nosComValor.RemoveAt(nosComValor.Count - 1);
            //linhas e colunas (10 registros por página e 7 colunas de informação)

            int registro = 0;
            _arrayNodes = new string[10, 7];
            foreach (var no in nosComValor)
            {
                var (valor, conteudo, nome) = await ShowNode(no);


                if (nome == "HR")
                {
                    registro = registro + 1;
                    continue;
                }

                //Nome
                if (nome == "B")
                {
                    _arrayNodes[registro, 1] = conteudo;
                    continue;
                }

                //Cargo/Inscrição
                if (valor.Contains("- Insc"))
                {
                    _arrayNodes[registro, 0] = valor;
                    continue;
                }

                //Situação
                if (valor.Contains("Sit"))
                {
                    _arrayNodes[registro, 2] = valor;
                    continue;
                }

                //Tipo de Inscrição
                if (valor.Contains("Tip"))
                {
                    _arrayNodes[registro, 3] = valor;
                    continue;
                }

                //Especialidade
                if (valor.Contains("Espec"))
                {
                    _arrayNodes[registro, 4] = valor;
                    continue;
                }

                //Data de Inscrição
                if (valor.Contains("Data de inscri"))
                {
                    _arrayNodes[registro, 5] = valor;
                    continue;
                }

                //Data de Registro
                if (valor.Contains("Data de regis"))
                {
                    _arrayNodes[registro, 6] = valor;
                    continue;
                }
            }

            _page = _page + 1;

            if (_pages >= _page)
                await StartByWebSiteCFO();
        }
        static async Task AddElement(int line, int column, string value)
        {

        }

        static async Task<(string, string, string)> ShowNode(INode node)
        {
            if (_enabledLogs)
            {
                Console.WriteLine($"|====================================================================");
                Console.WriteLine($"|  Conteudo: {node.TextContent}");
                Console.WriteLine($"|  Tipo: {node.NodeType}");
                Console.WriteLine($"|  Valor: {node.NodeValue}");
                Console.WriteLine($"|  Nome: {node.NodeName}");
                Console.WriteLine($"|====================================================================");
            }

            return (node.NodeValue, node.TextContent, node.NodeName);
        }

        static async Task updatePageOfUrl() => _url = _urlBase + $"?num_pagina={_page}&nome=CLEOMAR";

        static async Task Main(string[] args)
        {
            await StartByWebSiteCFO();

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


            //var altaResolucao = Stopwatch.IsHighResolution;
            //Stopwatch stopWatch = new Stopwatch();

            //stopWatch.Start();
            //Thread.Sleep(5000);
            //stopWatch.Stop();
            //TimeSpan ts = stopWatch.Elapsed;
            //string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            //    ts.Hours, ts.Minutes, ts.Seconds,
            //    ts.Milliseconds / 10);
            //Console.WriteLine(elapsedTime, "RunTime");

            Console.WriteLine("Finalizado a Leitura da Página!");
        }
    }

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
}
