using System;
using System.Collections.Generic;

public interface IPagamento
{
    string Tipo { get; }
    bool Processar(decimal valor);
}

public class PagamentoCartao : IPagamento
{
    public string Tipo => "Cartão de Crédito";
    public bool Processar(decimal valor)
    {
        Console.WriteLine($"[Cartão] Processando R$ {valor:F2} via operadora...");
        return true;
    }
}

public class PagamentoPix : IPagamento
{
    public string Tipo => "PIX";
    public bool Processar(decimal valor)
    {
        Console.WriteLine($"[PIX] Transferindo R$ {valor:F2} via Banco Central...");
        return true;
    }
}

public class PagamentoBoleto : IPagamento
{
    public string Tipo => "Boleto";
    public bool Processar(decimal valor)
    {
        Console.WriteLine($"[Boleto] Gerando boleto de R$ {valor:F2} com vencimento em 3 dias...");
        return true;
    }
}

// Fábrica: decide qual objeto criar com base no tipo
public abstract class FabricaPagamento
{
    public abstract IPagamento CriarPagamento();
}

public class FabricaCartao : FabricaPagamento
{
    public override IPagamento CriarPagamento() => new PagamentoCartao();
}

public class FabricaPix : FabricaPagamento
{
    public override IPagamento CriarPagamento() => new PagamentoPix();
}

public class FabricaBoleto : FabricaPagamento
{
    public override IPagamento CriarPagamento() => new PagamentoBoleto();
}

// Registro central das fábricas disponíveis
public static class RegistroFabricas
{
    private static readonly Dictionary<string, FabricaPagamento> _fabricas =
        new Dictionary<string, FabricaPagamento>(StringComparer.OrdinalIgnoreCase)
        {
            { "cartao",  new FabricaCartao()  },
            { "pix",     new FabricaPix()     },
            { "boleto",  new FabricaBoleto()  },
        };

    public static IPagamento Criar(string tipo)
    {
        if (_fabricas.TryGetValue(tipo, out var fabrica))
            return fabrica.CriarPagamento();

        throw new ArgumentException($"Tipo de pagamento desconhecido: '{tipo}'");
    }
}


public class GerenciadorTransacoes
{
    // Lazy<T> garante inicialização tardia e thread-safety
    private static readonly Lazy<GerenciadorTransacoes> _instancia =
        new Lazy<GerenciadorTransacoes>(() => new GerenciadorTransacoes());

    private readonly List<string> _historico = new List<string>();
    private int _contador = 0;

    private GerenciadorTransacoes()
    {
        Console.WriteLine("[Singleton] GerenciadorTransacoes criado.");
    }

    public static GerenciadorTransacoes Instancia => _instancia.Value;

    public string RegistrarTransacao(string tipo, decimal valor, bool sucesso)
    {
        _contador++;
        string id = $"TXN-{_contador:D4}";
        string status = sucesso ? "APROVADA" : "RECUSADA";
        string registro = $"{id} | {tipo} | R$ {valor:F2} | {status}";
        _historico.Add(registro);
        return id;
    }

    public void ExibirHistorico()
    {
        Console.WriteLine("\n===== HISTÓRICO DE TRANSAÇÕES =====");
        if (_historico.Count == 0)
        {
            Console.WriteLine("Nenhuma transação registrada.");
            return;
        }
        foreach (var t in _historico)
            Console.WriteLine("  " + t);
        Console.WriteLine("===================================\n");
    }
}


public class ProxyPagamento : IPagamento
{
    private readonly IPagamento _pagamentoReal;
    private readonly decimal _saldoDisponivel;
    private readonly bool _contaBloqueada;

    public string Tipo => _pagamentoReal.Tipo;

    public ProxyPagamento(IPagamento pagamentoReal,
                          decimal saldoDisponivel,
                          bool contaBloqueada = false)
    {
        _pagamentoReal    = pagamentoReal;
        _saldoDisponivel  = saldoDisponivel;
        _contaBloqueada   = contaBloqueada;
    }

    public bool Processar(decimal valor)
    {
        Console.WriteLine($"[Proxy] Iniciando validações para {Tipo}...");

        if (_contaBloqueada)
        {
            Console.WriteLine("[Proxy] BLOQUEADO: conta marcada como suspeita de fraude.");
            return false;
        }

        if (valor <= 0)
        {
            Console.WriteLine("[Proxy] BLOQUEADO: valor inválido.");
            return false;
        }

        if (valor > _saldoDisponivel)
        {
            Console.WriteLine($"[Proxy] BLOQUEADO: saldo insuficiente " +
                              $"(disponível: R$ {_saldoDisponivel:F2}).");
            return false;
        }

        Console.WriteLine("[Proxy] Validações OK — delegando ao processador real.");
        return _pagamentoReal.Processar(valor);
    }
}


public interface IGatewayPagamento
{
    bool EnviarPagamento(decimal valor, string descricao);
}

// API legada do gateway externo — interface incompatível
public class GatewayStripeLegado
{
    public int ExecutarCobranca(double quantia, string moeda, string memo)
    {
        Console.WriteLine($"[Stripe Legado] Cobrança de {quantia} {moeda} — '{memo}'");
        return 200; // HTTP 200 OK
    }
}

// Adaptador: faz o GatewayStripeLegado parecer um IGatewayPagamento
public class AdaptadorStripe : IGatewayPagamento
{
    private readonly GatewayStripeLegado _stripe;

    public AdaptadorStripe(GatewayStripeLegado stripe)
    {
        _stripe = stripe;
    }

    public bool EnviarPagamento(decimal valor, string descricao)
    {
        Console.WriteLine("[Adaptador] Convertendo chamada para o formato Stripe...");
        int statusCode = _stripe.ExecutarCobranca((double)valor, "BRL", descricao);
        return statusCode == 200;
    }
}

// Outro gateway externo com interface diferente
public class GatewayPayPalExterno
{
    public string RealizarDebito(string currency, decimal amount, string note)
    {
        Console.WriteLine($"[PayPal] Debitando {amount} {currency} — nota: '{note}'");
        return "SUCCESS";
    }
}

public class AdaptadorPayPal : IGatewayPagamento
{
    private readonly GatewayPayPalExterno _paypal;

    public AdaptadorPayPal(GatewayPayPalExterno paypal)
    {
        _paypal = paypal;
    }

    public bool EnviarPagamento(decimal valor, string descricao)
    {
        Console.WriteLine("[Adaptador] Convertendo chamada para o formato PayPal...");
        string resultado = _paypal.RealizarDebito("BRL", valor, descricao);
        return resultado == "SUCCESS";
    }
}


public class FachadaPagamento
{
    private readonly decimal _saldoCliente;
    private readonly bool _contaBloqueada;
    private readonly IGatewayPagamento _gateway;

    public FachadaPagamento(decimal saldoCliente,
                            bool contaBloqueada = false,
                            IGatewayPagamento? gateway = null)
    {
        _saldoCliente   = saldoCliente;
        _contaBloqueada = contaBloqueada;
        _gateway        = gateway ?? new AdaptadorStripe(new GatewayStripeLegado());
    }

    // Único método que o cliente precisa chamar
    public bool RealizarPagamento(string tipoPagamento, decimal valor)
    {
        Console.WriteLine($"\n{'=',60}");
        Console.WriteLine($" FACHADA › Pagamento: {tipoPagamento.ToUpper()} | R$ {valor:F2}");
        Console.WriteLine($"{'=',60}");

        // 1. Fábrica cria o pagamento correto
        IPagamento pagamento = RegistroFabricas.Criar(tipoPagamento);

        // 2. Proxy protege o acesso
        IPagamento pagamentoProtegido =
            new ProxyPagamento(pagamento, _saldoCliente, _contaBloqueada);

        // 3. Decoradores enriquecem o comportamento
        IPagamento pagamentoDecorado =
            new DecoradorNotificacao(
                new DecoradorLog(pagamentoProtegido));

        // 4. Processa e registra no Singleton
        bool sucesso = pagamentoDecorado.Processar(valor);

        // 5. Envia ao gateway externo (se aprovado)
        if (sucesso)
            _gateway.EnviarPagamento(valor, $"Pagamento via {tipoPagamento}");

        // 6. Registra no histórico centralizado
        GerenciadorTransacoes.Instancia
            .RegistrarTransacao(pagamentoDecorado.Tipo, valor, sucesso);

        Console.WriteLine($" Resultado final: {(sucesso ? "✓ APROVADO" : "✗ RECUSADO")}");
        return sucesso;
    }
}


// Classe base abstrata do decorador
public abstract class DecoradorPagamento : IPagamento
{
    protected readonly IPagamento _pagamento;

    protected DecoradorPagamento(IPagamento pagamento)
    {
        _pagamento = pagamento;
    }

    public virtual string Tipo => _pagamento.Tipo;

    public virtual bool Processar(decimal valor)
        => _pagamento.Processar(valor);
}

// Decorador de Log: registra tempo e resultado de cada operação
public class DecoradorLog : DecoradorPagamento
{
    public DecoradorLog(IPagamento pagamento) : base(pagamento) { }

    public override bool Processar(decimal valor)
    {
        var inicio = DateTime.Now;
        Console.WriteLine($"[Log] Iniciando {Tipo} às {inicio:HH:mm:ss.fff}");

        bool resultado = base.Processar(valor);

        var duracao = (DateTime.Now - inicio).TotalMilliseconds;
        Console.WriteLine($"[Log] Concluído em {duracao:F1}ms — " +
                          $"Status: {(resultado ? "SUCESSO" : "FALHA")}");
        return resultado;
    }
}

// Decorador de Notificação: envia avisos após o processamento
public class DecoradorNotificacao : DecoradorPagamento
{
    public DecoradorNotificacao(IPagamento pagamento) : base(pagamento) { }

    public override bool Processar(decimal valor)
    {
        bool resultado = base.Processar(valor);

        if (resultado)
            Console.WriteLine($"[Notificação] ✉ E-mail enviado: " +
                              $"pagamento de R$ {valor:F2} aprovado.");
        else
            Console.WriteLine($"[Notificação] ⚠ SMS enviado: " +
                              $"pagamento de R$ {valor:F2} não processado.");

        return resultado;
    }
}

// Decorador de Taxa: aplica uma taxa extra
public class DecoradorTaxaInternacional : DecoradorPagamento
{
    private const decimal TaxaPercentual = 0.038m; // 3,8%

    public override string Tipo => $"{_pagamento.Tipo} (Internacional)";

    public DecoradorTaxaInternacional(IPagamento pagamento) : base(pagamento) { }

    public override bool Processar(decimal valor)
    {
        decimal taxa  = Math.Round(valor * TaxaPercentual, 2);
        decimal total = valor + taxa;
        Console.WriteLine($"[Taxa Internacional] Aplicando {TaxaPercentual:P1} → " +
                          $"R$ {valor:F2} + R$ {taxa:F2} = R$ {total:F2}");
        return base.Processar(total);
    }
}

class Program
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // ─── Cenário 1: Pagamentos normais via Fachada ──────────────
        Console.WriteLine("► CENÁRIO 1: Pagamentos aprovados (saldo R$ 1.000,00)");
        var fachada = new FachadaPagamento(saldoCliente: 1000m);
        fachada.RealizarPagamento("pix",    250.00m);
        fachada.RealizarPagamento("cartao", 499.90m);

        // ─── Cenário 2: Saldo insuficiente (Proxy bloqueia) ─────────
        Console.WriteLine("\n► CENÁRIO 2: Saldo insuficiente (saldo R$ 50,00)");
        var fachadaPobre = new FachadaPagamento(saldoCliente: 50m);
        fachadaPobre.RealizarPagamento("boleto", 200.00m);

        // ─── Cenário 3: Conta bloqueada (Proxy bloqueia) ─────────────
        Console.WriteLine("\n► CENÁRIO 3: Conta bloqueada por suspeita de fraude");
        var fachadaBloqueada = new FachadaPagamento(
            saldoCliente: 5000m, contaBloqueada: true);
        fachadaBloqueada.RealizarPagamento("cartao", 100.00m);

        // ─── Cenário 4: Gateway PayPal (Adaptador diferente) ────────
        Console.WriteLine("\n► CENÁRIO 4: Pagamento via gateway PayPal (Adaptador)");
        var gatewayPayPal = new AdaptadorPayPal(new GatewayPayPalExterno());
        var fachadaPayPal = new FachadaPagamento(saldoCliente: 2000m,
                                                  gateway: gatewayPayPal);
        fachadaPayPal.RealizarPagamento("cartao", 350.00m);

        // ─── Cenário 5: Decorador de taxa internacional manual ───────
        Console.WriteLine("\n► CENÁRIO 5: Decorador de taxa internacional (uso direto)");
        IPagamento pixBase       = RegistroFabricas.Criar("pix");
        IPagamento pixProtegido  = new ProxyPagamento(pixBase, 500m);
        IPagamento pixInternac   = new DecoradorTaxaInternacional(pixProtegido);
        IPagamento pixComLog     = new DecoradorLog(pixInternac);
        Console.WriteLine($"Tipo final: {pixComLog.Tipo}");
        pixComLog.Processar(100m);

        // ─── Singleton: exibe histórico completo ────────────────────
        GerenciadorTransacoes.Instancia.ExibirHistorico();

        Console.WriteLine("Pressione qualquer tecla para sair...");
        Console.ReadKey();
    }
}
