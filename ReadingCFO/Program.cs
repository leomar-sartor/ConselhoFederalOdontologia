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

namespace ReadingCFO
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string url = "https://website.cfo.org.br/profissionais-cadastrados/";
            string parametros = $"?cro=RJ&inscricao=36634";

            Console.WriteLine("Inciando Leitura da Página!");

            var context = BrowsingContext.New(
               Configuration.Default.WithDefaultLoader()
               .WithDefaultCookies()
               );

            var Page = await context.OpenAsync(url);

            var form = Page.Forms["formConsulta"];

            var fieldEnrollment = form["inscricao"] as IHtmlInputElement;

            var fieldName = form["nome"] as IHtmlInputElement;
            fieldName.Value = " ";

            var resultado = await form.SubmitAsync();
            var Results = resultado.GetElementsByClassName("entry-content").FirstOrDefault();
            Results.QuerySelector("div").Remove();
            Results.QuerySelector("script").Remove();

            var Atualizacao = Results.GetElementsByTagName("h6").FirstOrDefault();
            var ultimaAtualizacao = Atualizacao.InnerHtml;

            Results.QuerySelector("h6").Remove();

            //Eliminar Nós sem Valor e Deixar HR - Usa o <HR> como separador 
            var nosComValor = Results.ChildNodes
                .Where(n => !String.IsNullOrWhiteSpace(n.TextContent) && n.NodeName != "HR" && n.NodeName != "A")
                .ToList();

            var totailPagina = nosComValor[0].TextContent;
            var total = Int32.Parse(String.Join("", Regex.Split(totailPagina, @"[^\d]")));
            Console.WriteLine($"Total de Registros encontrados: {total}");

            //var totalDePaginas = total / 10;

            nosComValor.RemoveAt(0);
            //Remover a Paginação
            nosComValor.RemoveAt(nosComValor.Count - 1);
            nosComValor.RemoveAt(nosComValor.Count - 1);

            var quebrados = nosComValor.Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / 6)
                    .Select(x => x.Select(v => v.Value).ToList())
                    .ToList();

            var lista = new List<Medico>();

            Parallel.ForEach(quebrados, new ParallelOptions { MaxDegreeOfParallelism = 10 }, reg =>
            {
                lista.Add(new Medico(
                    reg[0].TextContent.Split("-")[0],
                    reg[0].TextContent.Split("-")[1].Split(":")[1],
                    reg[1].TextContent,
                    reg[2].TextContent.Split(":")[1],
                    reg[3].TextContent.Split(":")[1],
                    reg[4].TextContent.Split(":")[1].Trim(),
                    reg[5].TextContent.Split(":")[1].Trim()
                    ));
            });


            Thread.Sleep(3000);

            Console.WriteLine("Finalizado a Leitura da Página!");


            //Parte de ir para a próxima página




        }

       
    }

    public class Medico
    {
        public Medico(string funcao, string inscricao, string nome, string situacao, string tipo, string inscricaoCRO, string registroCRO)
        {
            Funcao = funcao;
            Inscricao = inscricao;
            Nome = nome;
            Situacao = situacao;
            Tipo = tipo;

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
        public string Situacao { get; set; }
        public string Tipo { get; set; }
        public string Nome { get; set; }
        public DateTime InscricaoCRO { get; set; }
        public DateTime RegistroCRO { get; set; }
    }
}
