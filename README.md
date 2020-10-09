[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_KCPTransport&metric=alert_status)](https://sonarcloud.io/dashboard?id=MirrorNG_KCPTransport)
[![SonarCloud Coverage](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_KCPTransport&metric=coverage)](https://sonarcloud.io/component_measures?id=MirrorNG_KCPTransport&metric=coverage)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_KCPTransport&metric=ncloc)](https://sonarcloud.io/dashboard?id=MirrorNG_KCPTransport)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_KCPTransport&metric=sqale_index)](https://sonarcloud.io/dashboard?id=MirrorNG_KCPTransport)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=MirrorNG_KCPTransport&metric=code_smells)](https://sonarcloud.io/dashboard?id=MirrorNG_KCPTransport)

# KCPTransport
A transport for MirrorNG using KCP on top of UDP

This is a fork of https://github.com/limpo1989/kcp-csharp

It is a pure C# KCP implementation.

KCP is a reliable algorithm on top of an unreliable transport (such as UDP). KCP is said to have lower latency than other reliable transports such as ENET and TCP.