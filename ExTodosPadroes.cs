using System;
using System.Collections.Generic;

public class MetricasServidor
{
    public string Servidor   { get; init; } = "";
    public double CpuPct     { get; init; }   // 0–100
    public double MemoriaPct { get; init; }   // 0–100
    public double DiscoPct   { get; init; }   // 0–100
    public DateTime ColetadoEm { get; init; } = DateTime.Now;

    public override string ToString() =>
        $"CPU={CpuPct:F1}%  MEM={MemoriaPct:F1}%  DISCO={DiscoPct:F1}%";
}

// Contrato de todo coletor de métricas
public interface IColetor
{
    string TipoServidor { get; }
    MetricasServidor Coletar(string nomeServidor);
}

// Coletor para servidores Web
public class ColetorWeb : IColetor
{
    public string TipoServidor => "Web";

    public MetricasServidor Coletar(string nomeServidor)
    {
        Console.WriteLine($"  [ColetorWeb] Coletando métricas de '{nomeServidor}'...");
        // Simula leitura real via agente HTTP
        return new MetricasServidor
        {
            Servidor    = nomeServidor,
            CpuPct      = SimularValor(20, 85),
            MemoriaPct  = SimularValor(40, 75),
            DiscoPct    = SimularValor(30, 60),
        };
    }

    private static double SimularValor(double min, double max)
    {
        var rng = new Random();
        return Math.Round(min + rng.NextDouble() * (max - min), 1);
    }
}

// Coletor para servidores de Banco de Dados
public class ColetorBancoDados : IColetor
{
    public string TipoServidor => "Banco de Dados";

    public MetricasServidor Coletar(string nomeServidor)
    {
        Console.WriteLine($"  [ColetorDB] Coletando métricas de '{nomeServidor}'...");
        var rng = new Random();
        return new MetricasServidor
        {
            Servidor    = nomeServidor,
            CpuPct      = Math.Round(30 + rng.NextDouble() * 60, 1),
            MemoriaPct  = Math.Round(55 + rng.NextDouble() * 40, 1),
            DiscoPct    = Math.Round(20 + rng.NextDouble() * 70, 1),
        };
    }
}

// Coletor para servidores de Cache (Redis, Memcached…)
public class ColetorCache : IColetor
{
    public string TipoServidor => "Cache";

    public MetricasServidor Coletar(string nomeServidor)
    {
        Console.WriteLine($"  [ColetorCache] Coletando métricas de '{nomeServidor}'...");
        var rng = new Random();
        return new MetricasServidor
        {
            Servidor    = nomeServidor,
            CpuPct      = Math.Round(5  + rng.NextDouble() * 30, 1),
            MemoriaPct  = Math.Round(70 + rng.NextDouble() * 28, 1),
            DiscoPct    = Math.Round(5  + rng.NextDouble() * 15, 1),
        };
    }
}

// Fábricas abstratas e concretas
public abstract class FabricaColetor
{
    public abstract IColetor CriarColetor();
}

public class FabricaColetorWeb       : FabricaColetor { public override IColetor CriarColetor() => new ColetorWeb();        }
public class FabricaColetorBancoDados: FabricaColetor { public override IColetor CriarColetor() => new ColetorBancoDados(); }
public class FabricaColetorCache     : FabricaColetor { public override IColetor CriarColetor() => new ColetorCache();      }

// Registro central — cliente pede pelo tipo como string
public static class RegistroColetores
{
    private static readonly Dictionary<string, FabricaColetor> _fabricas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "web",    new FabricaColetorWeb()        },
            { "db",     new FabricaColetorBancoDados() },
            { "cache",  new FabricaColetorCache()      },
        };

    public static IColetor Criar(string tipo)
    {
        if (_fabricas.TryGetValue(tipo, out var f)) return f.CriarColetor();
        throw new ArgumentException($"Tipo de servidor desconhecido: '{tipo}'");
    }
}

public class RepositorioMetricas
{
    private static readonly Lazy<RepositorioMetricas> _instancia =
        new(() => new RepositorioMetricas());

    private readonly List<MetricasServidor> _registros = new();

    private RepositorioMetricas()
    {
        Console.WriteLine("[Singleton] RepositorioMetricas inicializado.");
    }

    public static RepositorioMetricas Instancia => _instancia.Value;

    public void Salvar(MetricasServidor m)
    {
        _registros.Add(m);
    }

    public IReadOnlyList<MetricasServidor> TodosRegistros => _registros.AsReadOnly();

    public void ExibirResumo()
    {
        Console.WriteLine("\n====== REPOSITÓRIO DE MÉTRICAS ======");
        if (_registros.Count == 0) { Console.WriteLine("  (vazio)"); return; }
        foreach (var r in _registros)
            Console.WriteLine($"  [{r.ColetadoEm:HH:mm:ss}] {r.Servidor,-20} {r}");
        Console.WriteLine($"  Total de registros: {_registros.Count}");
        Console.WriteLine("=====================================\n");
    }
}

public class ProxyColetor : IColetor
{
    private readonly IColetor _coletorReal;
    private readonly string   _tokenValido;
    private readonly TimeSpan _intervaloMinimo;
    private DateTime          _ultimaColeta = DateTime.MinValue;

    public string TipoServidor => _coletorReal.TipoServidor;

    public ProxyColetor(IColetor coletorReal,
                        string   tokenValido,
                        TimeSpan intervaloMinimo)
    {
        _coletorReal     = coletorReal;
        _tokenValido     = tokenValido;
        _intervaloMinimo = intervaloMinimo;
    }

    public MetricasServidor Coletar(string nomeServidor)
    {
        Console.WriteLine($"[Proxy] Verificando acesso para '{nomeServidor}'...");

        // Verificação de autenticação
        if (_tokenValido != SessaoAtual.Token)
        {
            Console.WriteLine("[Proxy] NEGADO: token inválido ou ausente.");
            return null!;
        }

        // Rate-limit
        var decorrido = DateTime.Now - _ultimaColeta;
        if (decorrido < _intervaloMinimo)
        {
            Console.WriteLine($"[Proxy] NEGADO: aguarde " +
                $"{(_intervaloMinimo - decorrido).TotalSeconds:F1}s antes da próxima coleta.");
            return null!;
        }

        Console.WriteLine("[Proxy] Acesso autorizado — delegando coleta.");
        _ultimaColeta = DateTime.Now;
        return _coletorReal.Coletar(nomeServidor);
    }
}

// Simula sessão global do usuário autenticado
public static class SessaoAtual
{
    public static string Token { get; set; } = "";
}

// Interface interna que o sistema usa
// (já definida acima como IColetor)

// --- API do Zabbix (legada, formato incompatível) ---
public class ZabbixApiLegada
{
    public Dictionary<string, double> ObterDadosHost(string host)
    {
        Console.WriteLine($"  [Zabbix API] Consultando host '{host}'...");
        var rng = new Random();
        return new Dictionary<string, double>
        {
            { "system.cpu.load",  Math.Round(rng.NextDouble() * 90, 1) },
            { "vm.memory.size",   Math.Round(50 + rng.NextDouble() * 45, 1) },
            { "vfs.fs.size",      Math.Round(20 + rng.NextDouble() * 75, 1) },
        };
    }
}

public class AdaptadorZabbix : IColetor
{
    private readonly ZabbixApiLegada _zabbix;
    public string TipoServidor => "Zabbix (Adaptado)";

    public AdaptadorZabbix(ZabbixApiLegada zabbix) => _zabbix = zabbix;

    public MetricasServidor Coletar(string nomeServidor)
    {
        Console.WriteLine("[Adaptador] Convertendo dados do Zabbix → MetricasServidor...");
        var dados = _zabbix.ObterDadosHost(nomeServidor);
        return new MetricasServidor
        {
            Servidor    = nomeServidor,
            CpuPct      = dados["system.cpu.load"],
            MemoriaPct  = dados["vm.memory.size"],
            DiscoPct    = dados["vfs.fs.size"],
        };
    }
}

// --- API do Datadog (formato diferente, baseado em JSON-like objects) ---
public class DatadogCliente
{
    public record DatadogMetric(string MetricName, double Value, string Host);

    public List<DatadogMetric> QueryMetrics(string hostname, string[] metricNames)
    {
        Console.WriteLine($"  [Datadog] Querying metrics for '{hostname}'...");
        var rng = new Random();
        return new List<DatadogMetric>
        {
            new("system.cpu.user",   Math.Round(rng.NextDouble() * 95, 1), hostname),
            new("system.mem.used",   Math.Round(30 + rng.NextDouble() * 65, 1), hostname),
            new("system.disk.in_use",Math.Round(10 + rng.NextDouble() * 80, 1), hostname),
        };
    }
}

public class AdaptadorDatadog : IColetor
{
    private readonly DatadogCliente _datadog;
    public string TipoServidor => "Datadog (Adaptado)";

    public AdaptadorDatadog(DatadogCliente datadog) => _datadog = datadog;

    public MetricasServidor Coletar(string nomeServidor)
    {
        Console.WriteLine("[Adaptador] Convertendo dados do Datadog → MetricasServidor...");
        var metricas = _datadog.QueryMetrics(nomeServidor,
            new[] { "system.cpu.user", "system.mem.used", "system.disk.in_use" });

        double Get(string name) =>
            metricas.Find(m => m.MetricName == name)?.Value ?? 0;

        return new MetricasServidor
        {
            Servidor    = nomeServidor,
            CpuPct      = Get("system.cpu.user"),
            MemoriaPct  = Get("system.mem.used"),
            DiscoPct    = Get("system.disk.in_use"),
        };
    }
}

public enum NivelAlerta { Ok, Aviso, Critico }

public class ResultadoAnalise
{
    public NivelAlerta Nivel   { get; init; }
    public string      Resumo  { get; init; } = "";
    public List<string> Causas { get; init; } = new();
}

public interface IEstrategiaAnalise
{
    string Nome { get; }
    ResultadoAnalise Analisar(MetricasServidor m);
}

// Estratégia conservadora — usada em produção (limites baixos)
public class AnalisePrducao : IEstrategiaAnalise
{
    public string Nome => "Análise Produção (conservadora)";

    public ResultadoAnalise Analisar(MetricasServidor m)
    {
        var causas = new List<string>();
        var nivel  = NivelAlerta.Ok;

        if (m.CpuPct      > 70) { causas.Add($"CPU em {m.CpuPct}% (limite 70%)");      nivel = NivelAlerta.Aviso;   }
        if (m.MemoriaPct  > 75) { causas.Add($"Memória em {m.MemoriaPct}% (limite 75%)"); nivel = NivelAlerta.Aviso; }
        if (m.DiscoPct    > 60) { causas.Add($"Disco em {m.DiscoPct}% (limite 60%)");   nivel = NivelAlerta.Aviso;   }
        if (m.CpuPct      > 90) nivel = NivelAlerta.Critico;
        if (m.MemoriaPct  > 90) nivel = NivelAlerta.Critico;
        if (m.DiscoPct    > 85) nivel = NivelAlerta.Critico;

        return new ResultadoAnalise
        {
            Nivel  = nivel,
            Resumo = nivel == NivelAlerta.Ok ? "Servidor saudável" : $"Atenção necessária",
            Causas = causas,
        };
    }
}

// Estratégia permissiva — usada em desenvolvimento (limites altos)
public class AnaliseDev : IEstrategiaAnalise
{
    public string Nome => "Análise Dev (permissiva)";

    public ResultadoAnalise Analisar(MetricasServidor m)
    {
        var causas = new List<string>();
        var nivel  = NivelAlerta.Ok;

        if (m.CpuPct     > 90) { causas.Add($"CPU crítica: {m.CpuPct}%");     nivel = NivelAlerta.Critico; }
        if (m.MemoriaPct > 95) { causas.Add($"Memória crítica: {m.MemoriaPct}%"); nivel = NivelAlerta.Critico; }
        if (m.DiscoPct   > 95) { causas.Add($"Disco crítico: {m.DiscoPct}%");  nivel = NivelAlerta.Critico; }

        return new ResultadoAnalise
        {
            Nivel  = nivel,
            Resumo = nivel == NivelAlerta.Ok ? "OK para desenvolvimento" : "Crítico mesmo em dev",
            Causas = causas,
        };
    }
}

// Estratégia ponderada — calcula score composto (média ponderada)
public class AnaliseScorePonderado : IEstrategiaAnalise
{
    public string Nome => "Análise por Score Ponderado";

    public ResultadoAnalise Analisar(MetricasServidor m)
    {
        // Pesos: CPU tem mais impacto que disco
        double score = m.CpuPct * 0.5 + m.MemoriaPct * 0.3 + m.DiscoPct * 0.2;
        var nivel = score switch
        {
            > 85 => NivelAlerta.Critico,
            > 65 => NivelAlerta.Aviso,
            _    => NivelAlerta.Ok,
        };

        return new ResultadoAnalise
        {
            Nivel  = nivel,
            Resumo = $"Score ponderado: {score:F1}",
            Causas = nivel != NivelAlerta.Ok
                ? new List<string> { $"Score {score:F1} acima do limiar ({(nivel == NivelAlerta.Critico ? 85 : 65)})" }
                : new List<string>(),
        };
    }
}

public class AlertaServidor
{
    public string          Servidor    { get; init; } = "";
    public NivelAlerta     Nivel       { get; init; }
    public string          Resumo      { get; init; } = "";
    public List<string>    Causas      { get; init; } = new();
    public DateTime        Gerado      { get; init; } = DateTime.Now;
}

public interface IObservadorAlerta
{
    void AoReceberAlerta(AlertaServidor alerta);
}

// Subject (publicador)
public class GerenciadorAlertas
{
    private readonly List<IObservadorAlerta> _observadores = new();

    public void Registrar(IObservadorAlerta obs)
    {
        _observadores.Add(obs);
        Console.WriteLine($"[Observer] Registrado: {obs.GetType().Name}");
    }

    public void Publicar(AlertaServidor alerta)
    {
        if (alerta.Nivel == NivelAlerta.Ok) return; // Sem alerta, sem notificação
        Console.WriteLine($"\n[Observer] Publicando alerta '{alerta.Nivel}' para " +
                          $"{_observadores.Count} observador(es)...");
        foreach (var obs in _observadores)
            obs.AoReceberAlerta(alerta);
    }
}

// Observador 1: Notificação via Slack
public class ObservadorSlack : IObservadorAlerta
{
    public void AoReceberAlerta(AlertaServidor a)
    {
        string emoji = a.Nivel == NivelAlerta.Critico ? "🚨" : "⚠️";
        Console.WriteLine($"  [Slack] {emoji} #{(a.Nivel == NivelAlerta.Critico ? "alertas-criticos" : "alertas-gerais")} " +
                          $"→ *{a.Servidor}*: {a.Resumo}");
        foreach (var c in a.Causas)
            Console.WriteLine($"           • {c}");
    }
}

// Observador 2: Disparo de e-mail para o time de ops
public class ObservadorEmail : IObservadorAlerta
{
    private readonly string _destinatario;

    public ObservadorEmail(string destinatario) => _destinatario = destinatario;

    public void AoReceberAlerta(AlertaServidor a)
    {
        Console.WriteLine($"  [E-mail] Para: {_destinatario} | Assunto: " +
                          $"[{a.Nivel.ToString().ToUpper()}] Alerta em {a.Servidor}");
    }
}

// Observador 3: PagerDuty — acorda alguém apenas em caso crítico
public class ObservadorPagerDuty : IObservadorAlerta
{
    public void AoReceberAlerta(AlertaServidor a)
    {
        if (a.Nivel != NivelAlerta.Critico) return;
        Console.WriteLine($"  [PagerDuty] 📟 INCIDENTE ABERTO: {a.Servidor} — {a.Resumo} " +
                          $"às {a.Gerado:HH:mm:ss}");
    }
}

// Observador 4: Painel de controle — acumula estatísticas
public class ObservadorPainel : IObservadorAlerta
{
    private int _avisos   = 0;
    private int _criticos = 0;

    public void AoReceberAlerta(AlertaServidor a)
    {
        if (a.Nivel == NivelAlerta.Aviso)   _avisos++;
        if (a.Nivel == NivelAlerta.Critico) _criticos++;
        Console.WriteLine($"  [Painel] Contadores atualizados — " +
                          $"Avisos: {_avisos}  Críticos: {_criticos}");
    }

    public void ExibirEstatisticas()
    {
        Console.WriteLine($"\n===== PAINEL DE ALERTAS =====");
        Console.WriteLine($"  ⚠  Avisos  : {_avisos}");
        Console.WriteLine($"  🚨 Críticos: {_criticos}");
        Console.WriteLine($"=============================\n");
    }
}

public abstract class DecoradorColetor : IColetor
{
    protected readonly IColetor _coletor;
    protected DecoradorColetor(IColetor coletor) => _coletor = coletor;

    public virtual string TipoServidor => _coletor.TipoServidor;
    public virtual MetricasServidor Coletar(string nomeServidor)
        => _coletor.Coletar(nomeServidor);
}

// Decorador de Log — mede e registra tempo de cada coleta
public class DecoradorLogColeta : DecoradorColetor
{
    public DecoradorLogColeta(IColetor coletor) : base(coletor) { }

    public override MetricasServidor Coletar(string nomeServidor)
    {
        var inicio = DateTime.Now;
        Console.WriteLine($"[DecoradorLog] ► Início coleta: {inicio:HH:mm:ss.fff}");
        var resultado = base.Coletar(nomeServidor);
        var ms = (DateTime.Now - inicio).TotalMilliseconds;
        Console.WriteLine($"[DecoradorLog] ◄ Coleta concluída em {ms:F1}ms");
        return resultado;
    }
}

// Decorador de Cache — reutiliza resultado por N segundos
public class DecoradorCache : DecoradorColetor
{
    private readonly Dictionary<string, (MetricasServidor dados, DateTime expira)> _cache = new();
    private readonly TimeSpan _ttl;

    public DecoradorCache(IColetor coletor, TimeSpan ttl) : base(coletor) => _ttl = ttl;

    public override MetricasServidor Coletar(string nomeServidor)
    {
        if (_cache.TryGetValue(nomeServidor, out var entrada) && DateTime.Now < entrada.expira)
        {
            Console.WriteLine($"[DecoradorCache] ✓ Cache hit para '{nomeServidor}' " +
                              $"(expira em {(entrada.expira - DateTime.Now).TotalSeconds:F0}s)");
            return entrada.dados;
        }

        Console.WriteLine($"[DecoradorCache] Cache miss — coletando dados frescos...");
        var resultado = base.Coletar(nomeServidor);
        if (resultado != null)
            _cache[nomeServidor] = (resultado, DateTime.Now.Add(_ttl));
        return resultado!;
    }
}

// Decorador de Compressão — simula redução de payload antes de salvar
public class DecoradorCompressao : DecoradorColetor
{
    public DecoradorCompressao(IColetor coletor) : base(coletor) { }

    public override MetricasServidor Coletar(string nomeServidor)
    {
        var resultado = base.Coletar(nomeServidor);
        if (resultado != null)
            Console.WriteLine($"[DecoradorCompressao] Payload comprimido (simulado): " +
                              $"~{new Random().Next(200, 500)}B → ~{new Random().Next(80, 150)}B");
        return resultado!;
    }
}

public class MonitorFachada
{
    private readonly string               _token;
    private readonly IEstrategiaAnalise   _estrategia;
    private readonly GerenciadorAlertas   _alertas;
    private readonly IColetor?            _coletorExterno; // Adaptador opcional

    public MonitorFachada(string             token,
                          IEstrategiaAnalise estrategia,
                          GerenciadorAlertas alertas,
                          IColetor?          coletorExterno = null)
    {
        _token          = token;
        _estrategia     = estrategia;
        _alertas        = alertas;
        _coletorExterno = coletorExterno;

        // Garante que o token da sessão está configurado
        SessaoAtual.Token = _token;
    }

    public void MonitorarServidor(string nomeServidor, string tipoServidor)
    {
        Console.WriteLine($"\n{'─',62}");
        Console.WriteLine($" MONITOR › {nomeServidor} [{tipoServidor.ToUpper()}]");
        Console.WriteLine($" Estratégia: {_estrategia.Nome}");
        Console.WriteLine($"{'─',62}");

        // 1. FÁBRICA — cria o coletor adequado (ou usa adaptador externo)
        IColetor coletorBase = _coletorExterno ?? RegistroColetores.Criar(tipoServidor);

        // 2. PROXY — controla autenticação e rate-limit
        IColetor comProxy = new ProxyColetor(coletorBase, _token,
                                             intervaloMinimo: TimeSpan.FromSeconds(0));

        // 3. DECORADORES — log + cache + compressão empilhados
        IColetor comDecorador = new DecoradorCompressao(
                                    new DecoradorCache(
                                        new DecoradorLogColeta(comProxy),
                                        ttl: TimeSpan.FromSeconds(30)));

        // 4. Coleta as métricas
        MetricasServidor metricas = comDecorador.Coletar(nomeServidor);
        if (metricas == null)
        {
            Console.WriteLine(" ✗ Coleta falhou (acesso negado ou rate-limit).");
            return;
        }

        Console.WriteLine($" Métricas: {metricas}");

        // 5. STRATEGY — analisa saúde com o algoritmo configurado
        ResultadoAnalise analise = _estrategia.Analisar(metricas);
        string icone = analise.Nivel switch
        {
            NivelAlerta.Ok      => "✓",
            NivelAlerta.Aviso   => "⚠",
            NivelAlerta.Critico => "✗",
            _                   => "?"
        };
        Console.WriteLine($" Análise [{icone}]: {analise.Resumo}");

        // 6. SINGLETON — persiste métricas no repositório central
        RepositorioMetricas.Instancia.Salvar(metricas);

        // 7. OBSERVER — publica alerta se necessário
        _alertas.Publicar(new AlertaServidor
        {
            Servidor = nomeServidor,
            Nivel    = analise.Nivel,
            Resumo   = analise.Resumo,
            Causas   = analise.Causas,
        });
    }
}

class Program
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // ── Configura observadores (Observer) ────────────────────────
        var alertas  = new GerenciadorAlertas();
        var painel   = new ObservadorPainel();
        alertas.Registrar(new ObservadorSlack());
        alertas.Registrar(new ObservadorEmail("ops@empresa.com"));
        alertas.Registrar(new ObservadorPagerDuty());
        alertas.Registrar(painel);

        Console.WriteLine("\n CENÁRIO 1: Produção — Estratégia Conservadora");
        var monitorProd = new MonitorFachada(
            token:      "TOKEN-VALIDO-PROD",
            estrategia: new AnalisePrducao(),
            alertas:    alertas);

        monitorProd.MonitorarServidor("web-01.prod",  "web");
        monitorProd.MonitorarServidor("db-master",    "db");
        monitorProd.MonitorarServidor("redis-cache",  "cache");

        Console.WriteLine("\n CENÁRIO 2: Dev — Estratégia Permissiva");
        var monitorDev = new MonitorFachada(
            token:      "TOKEN-VALIDO-DEV",
            estrategia: new AnaliseDev(),
            alertas:    alertas);

        monitorDev.MonitorarServidor("web-01.dev", "web");

        Console.WriteLine("\n CENÁRIO 3: Score Ponderado");
        var monitorScore = new MonitorFachada(
            token:      "TOKEN-VALIDO-PROD",
            estrategia: new AnaliseScorePonderado(),
            alertas:    alertas);

        monitorScore.MonitorarServidor("app-server", "web");

        Console.WriteLine("\n CENÁRIO 4: Fonte Externa — Adaptador Zabbix");
        var zabbix = new AdaptadorZabbix(new ZabbixApiLegada());
        var monitorZabbix = new MonitorFachada(
            token:          "TOKEN-VALIDO-PROD",
            estrategia:     new AnalisePrducao(),
            alertas:        alertas,
            coletorExterno: zabbix);

        monitorZabbix.MonitorarServidor("legado-zabbix-01", "web");

        Console.WriteLine("\n CENÁRIO 5: Fonte Externa — Adaptador Datadog");
        var datadog = new AdaptadorDatadog(new DatadogCliente());
        var monitorDatadog = new MonitorFachada(
            token:          "TOKEN-VALIDO-PROD",
            estrategia:     new AnaliseScorePonderado(),
            alertas:        alertas,
            coletorExterno: datadog);

        monitorDatadog.MonitorarServidor("cloud-node-42", "web");

        Console.WriteLine("\n CENÁRIO 6: Token Inválido — Proxy Bloqueia");
        SessaoAtual.Token = "TOKEN-VALIDO-PROD"; // sessão atual válida
        var coletorBase   = RegistroColetores.Criar("web");
        var proxyInvalido = new ProxyColetor(coletorBase,
                                             tokenValido: "TOKEN-SECRETO",
                                             intervaloMinimo: TimeSpan.Zero);
        var resultado = proxyInvalido.Coletar("servidor-restrito");
        Console.WriteLine($" Resultado: {(resultado == null ? "✗ Acesso negado" : resultado.ToString())}");

        
        Console.WriteLine("\n CENÁRIO 7: Decoradores Empilhados (uso direto)");
        IColetor base7      = RegistroColetores.Criar("db");
        IColetor comLog     = new DecoradorLogColeta(base7);
        IColetor comCache   = new DecoradorCache(comLog, TimeSpan.FromSeconds(10));
        IColetor comCompr   = new DecoradorCompressao(comCache);

        Console.WriteLine("Primeira coleta (cache miss):");
        comCompr.Coletar("db-replica");

        Console.WriteLine("\nSegunda coleta (deve usar cache):");
        comCompr.Coletar("db-replica");

        RepositorioMetricas.Instancia.ExibirResumo();
        painel.ExibirEstatisticas();

        Console.WriteLine("Pressione qualquer tecla para sair...");
        Console.ReadKey();
    }
}
