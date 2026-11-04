# ADR-001. ASP.NET Core Razor Pages, com renderização no servidor

**Estado**: Aceite. **Data**: 30 de outubro de 2026.

## Contexto

A aplicação tem quatro funcionalidades, quatro ecrãs, e uma interface que consiste em
listas, um formulário e um botão. A interatividade necessária resume se a um botão que
muda de estado e a um número que incrementa.

As notas técnicas do briefing já indicavam Razor Pages como preferência, e o destino
de publicação é um IIS interno já existente.

## Decisão

ASP.NET Core Razor Pages, com renderização no servidor e o padrão POST/Redirect/GET.

O único JavaScript da aplicação é um pequeno script de melhoria progressiva que
intercepta a submissão do voto e troca o botão e a contagem no sítio. A aplicação
funciona por inteiro sem ele.

## Alternativas consideradas

**Blazor Server.** Daria interatividade sem recarregamento, mas acrescenta uma ligação
persistente por utilizador, estado de circuito a gerir, e comportamento de reconexão a
pensar. Para um botão que incrementa um número, é infraestrutura a mais pelo mesmo
resultado visível.

**Web API com uma aplicação de página única.** Dois artefactos para construir, publicar
e manter, uma cadeia de dependências de JavaScript a atualizar, e autenticação a
resolver duas vezes. O custo é permanente e o benefício não existe neste âmbito.

**Razor Pages com HTMX ou equivalente.** Resolveria o mesmo que o script próprio,
acrescentando uma dependência externa. O script necessário tem poucas dezenas de
linhas.

## Consequências

**Boas.** Um artefacto para publicar. Autenticação resolvida uma vez, por cookie.
Nenhuma dependência de JavaScript para manter atualizada. A aplicação continua
utilizável com JavaScript desativado, o que torna a melhoria progressiva uma
propriedade e não um esforço.

**Más.** Sem o script, cada voto recarrega a página, e a posição de leitura tem de ser
preservada com cuidado. Se a interface vier a precisar de interatividade a sério, como
atualização em tempo real do ranking enquanto outras pessoas votam, esta escolha passa
a ser limitativa e obriga a reconsiderar.

**Aceite, de propósito.** O ranking não atualiza sozinho. Quem estiver com a página
aberta vê os números do momento em que a carregou.
