using AngleSharp;
using System;
using System.Threading.Tasks;
using System.Linq;
using AngleSharp.Html.Dom;
using AngleSharp.Dom;
using System.Collections.Generic;

namespace ReadingCFO
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Inciando Leitura da Página!");

            var context = BrowsingContext.New(
               Configuration.Default.WithDefaultLoader()
               .WithDefaultCookies()
               );

            // https://website.cfo.org.br/ Serviços -> Consultas aos Profissionais e Entidades Cadastradas

            var Page = await context.OpenAsync("https://website.cfo.org.br/profissionais-cadastrados/");

            var form = Page.Forms["formConsulta"];

            //var elementsOfForm = form.Elements["inscricao"];
            var fieldEnrollment = form["inscricao"] as IHtmlInputElement;

            var fieldName = form["nome"] as IHtmlInputElement;
            fieldName.Value = "CLEOMAR ALVES DOS SANTOS";



            var resultado = await form.SubmitAsync();
            var Results = resultado.GetElementsByClassName("entry-content").FirstOrDefault();
            Results.QuerySelector("div").Remove();
            Results.QuerySelector("script").Remove();

            var Atualizacao = Results.GetElementsByTagName("h6").FirstOrDefault();
            var ultimaAtualizacao = Atualizacao.InnerHtml;

            Results.QuerySelector("h6").Remove();

            //var document = await context.OpenAsync(req => req.Content("<h1>Some example source</h1><p>This is a paragraph element"));

            var totalDeRegistros = 0;

            //Eliminar Nós sem Valor e Deixar HR - Usa o <HR> como separador 
            var nosComValor = Results.ChildNodes
                .Where(n => !String.IsNullOrWhiteSpace(n.TextContent) && n.NodeName != "HR" && n.NodeName != "A")
                .ToList();

            var totais = nosComValor[0].TextContent;
            nosComValor.RemoveAt(0);
            //Remover a PAginação
            nosComValor.RemoveAt(nosComValor.Count -1);
            nosComValor.RemoveAt(nosComValor.Count - 1);
            //nosComValor.RemoveAt(0);

            Console.WriteLine("Quantidade de Nós: " + nosComValor.Count());
            int cont = 1;

            var quebrados = nosComValor.Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / 6)
                    .Select(x => x.Select(v => v.Value).ToList())
                    .ToList();
            //.SelectMany(c => c);

            var lista = new List<Medico>();


            Parallel.ForEach(quebrados, new ParallelOptions { MaxDegreeOfParallelism = 10 }, reg =>
            {
                //reg.CalcularSaldoDepreciacao(Periodo);
                lista.Add(new Medico(
                    reg[0].TextContent.Split("-")[0],
                    reg[0].TextContent.Split("-")[1],
                    reg[1].TextContent,
                    reg[2].TextContent,
                    reg[3].TextContent,
                    new DateTime(), //reg[2].TextContent,
                    new DateTime() //reg[3].TextContent,
                    ));


            });

            //foreach (var child in quebrados)
            //{
                //var entrar = true;
                //if (entrar)
                //{
                //    if (child.TextContent.Contains("Totais"))
                //    {
                //        totalDeRegistros = 10;
                //        entrar = false;
                //        continue;
                //    }
                //}

                //// 6
                //var Um = child.TextContent;
                //var Dois = child.TextContent;


                //if (child.NodeName == "HR")
                //{

                //    //Adiciona o carinha e continua
                //}


                //Console.WriteLine($"Nome: {child.NodeName} / Texto: {child.TextContent}");
                
            //}


            Console.WriteLine("Finalizado a Leitura da Página!");

        }
    }


    public class Medico
    {
        public Medico(string funcao, string inscricao, string nome, string situacao, string tipo, DateTime inscricaoCRO, DateTime registroCRO)
        {
            Funcao = funcao;
            Inscricao = inscricao;
            Nome = nome;
            Situacao = Situacao;
            Tipo = tipo;
            InscricaoCRO = inscricaoCRO;
            RegistroCRO = registroCRO;

        }
        public string Funcao { get; set; }
        public string Inscricao { get; set; }

        public string Situacao { get; set; }
        public string Tipo { get; set; }
        public string Nome { get; set; }
        public DateTime InscricaoCRO { get; set; }
        public DateTime RegistroCRO { get; set; }

    }
}
