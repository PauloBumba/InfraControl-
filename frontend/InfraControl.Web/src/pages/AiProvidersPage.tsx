import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { PageHeader } from "@/components/ui/PageHeader";
import { FormField, FormError } from "@/components/ui/FormField";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { Button } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/StatusChip";
import { Can } from "@/components/auth/Can";
import { mutationErrorMessage } from "@/hooks/queries";
import {
  getAiProviders,
  createAiProvider,
  updateAiProvider,
  setAiProviderActive,
  upsertAiProviderCapability,
  deleteAiProviderCapability,
  testAiProviderConnection,
  listAiProviderModels,
  type AiProviderDto,
} from "@/services/api/aiProviders";

const inputStyle: React.CSSProperties = {
  padding: "7px 10px",
  fontSize: 13,
  border: "1px solid #d1d5db",
  borderRadius: 6,
  width: "100%",
};

function parseModels(value: string): string[] | null {
  const models = value
    .split(",")
    .map((item) => item.trim())
    .filter((item) => item !== "");
  return models.length > 0 ? models : null;
}

/**
 * FAI-T011b (ADR-057) — Administração → Providers de IA.
 * Cadastro-first (D3): linha inativa/ausente controla o resolve do router;
 * a UI só reflete o que o backend decide. Chave write-only com máscara
 * `••••XXXX`; campo vazio no form = preserva a gravada. Escrita atrás de
 * `Can level="administer"`; backend continua sendo a autoridade.
 */
export function AiProvidersPage() {
  const queryClient = useQueryClient();
  const queryKey = ["ai-providers"];

  const list = useQuery({ queryKey, queryFn: getAiProviders });

  const [saveError, setSaveError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [testResults, setTestResults] = useState<Record<string, { ok: boolean; error: string | null }>>({});
  const [modelResults, setModelResults] = useState<Record<string, { models: string[]; error: string | null }>>({});

  const [showCreate, setShowCreate] = useState(false);
  const [newName, setNewName] = useState("");
  const [newBaseUrl, setNewBaseUrl] = useState("");
  const [newApiKey, setNewApiKey] = useState("");
  const [newModels, setNewModels] = useState("");

  const [editId, setEditId] = useState<string | null>(null);
  const [editBaseUrl, setEditBaseUrl] = useState("");
  const [editApiKey, setEditApiKey] = useState("");
  const [editModels, setEditModels] = useState("");

  const [routeDrafts, setRouteDrafts] = useState<
    Record<string, { providerId: string; defaultModel: string; fallbackProviderId: string; fallbackModel: string }>
  >({});

  const invalidate = () => queryClient.invalidateQueries({ queryKey });

  const notify = (message: string, error?: Error | null) => {
    setSaveError(null);
    setNotice(message);
    if (error) setSaveError(mutationErrorMessage(error));
  };

  const create = useMutation({
    mutationFn: createAiProvider,
    onSuccess: (provider) => {
      notify(`Provider "${provider.name}" cadastrado.`);
      setShowCreate(false);
      setNewName("");
      setNewBaseUrl("");
      setNewApiKey("");
      setNewModels("");
      void invalidate();
    },
    onError: (error) => {
      setNotice(null);
      setSaveError(mutationErrorMessage(error as Error));
    },
  });

  const update = useMutation({
    mutationFn: ({ id, baseUrl, apiKey, models }: { id: string; baseUrl: string; apiKey: string | null; models: string[] | null }) =>
      updateAiProvider(id, { baseUrl, apiKey, models }),
    onSuccess: (provider) => {
      notify(`Provider "${provider.name}" atualizado.`);
      setEditId(null);
      setEditApiKey("");
      void invalidate();
    },
    onError: (error) => {
      setNotice(null);
      setSaveError(mutationErrorMessage(error as Error));
    },
  });

  const toggleActive = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setAiProviderActive(id, active),
    onSuccess: (provider) => {
      notify(
        provider.active
          ? `Provider "${provider.name}" ativado.`
          : `Provider "${provider.name}" desativado — capacidades sem rota caem no config ou no estado "não configurado".`
      );
      void invalidate();
    },
    onError: (error) => {
      setNotice(null);
      setSaveError(mutationErrorMessage(error as Error));
    },
  });

  const saveRoute = useMutation({
    mutationFn: ({ capability, providerId, defaultModel, fallbackProviderId, fallbackModel }: {
      capability: string;
      providerId: string;
      defaultModel: string;
      fallbackProviderId: string | null;
      fallbackModel: string | null;
    }) =>
      upsertAiProviderCapability(capability, {
        providerId,
        defaultModel,
        fallbackProviderId,
        fallbackModel,
      }),
    onSuccess: (route) => {
      notify(`Rota da capacidade "${route.capability}" salva.`);
      void invalidate();
    },
    onError: (error) => {
      setNotice(null);
      setSaveError(mutationErrorMessage(error as Error));
    },
  });

  const clearRoute = useMutation({
    mutationFn: (capability: string) => deleteAiProviderCapability(capability),
    onSuccess: () => {
      notify("Rota removida — a capacidade volta ao fallback de configuração.");
      void invalidate();
    },
    onError: (error) => {
      setNotice(null);
      setSaveError(mutationErrorMessage(error as Error));
    },
  });

  const test = useMutation({
    mutationFn: (id: string) => testAiProviderConnection(id),
    onSuccess: (result, id) => {
      setTestResults((prev) => ({ ...prev, [id]: result }));
    },
    onError: (error, id) => {
      setTestResults((prev) => ({
        ...prev,
        [id]: { ok: false, error: mutationErrorMessage(error as Error) },
      }));
    },
  });

  const models = useMutation({
    mutationFn: (id: string) => listAiProviderModels(id),
    onSuccess: (result, id) => {
      setModelResults((prev) => ({ ...prev, [id]: result }));
      if (result.error === null) {
        setNewModels(result.models.join(", "));
        setEditModels(result.models.join(", "));
      }
    },
    onError: (error, id) => {
      setModelResults((prev) => ({
        ...prev,
        [id]: { models: [], error: mutationErrorMessage(error as Error) },
      }));
    },
  });

  function startEdit(provider: AiProviderDto) {
    setEditId(provider.id);
    setEditBaseUrl(provider.baseUrl);
    setEditApiKey("");
    setEditModels(provider.models.join(", "));
    setSaveError(null);
  }

  if (list.isLoading) return <LoadingBlock />;

  const providers = list.data?.providers ?? [];
  const capabilities = list.data?.capabilities ?? [];
  const adapters = list.data?.availableAdapterNames ?? [];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Providers de IA"
        subtitle="Administração → IA · Cadastro-first (ADR-057): o resolve usa a linha do banco; sem linha cai no config"
        actions={
          <Can level="administer">
            <Button
              type="button"
              variant="primary"
              style={{ padding: "6px 14px" }}
              onClick={() => setShowCreate((value) => !value)}
            >
              {showCreate ? "Cancelar" : "Cadastrar provider"}
            </Button>
          </Can>
        }
      />

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}
      {saveError && <ErrorBanner message={saveError} onRetry={() => setSaveError(null)} />}

      {showCreate && (
        <Can level="administer">
          <div className="uni-panel" style={{ padding: 16 }}>
            <form
              onSubmit={(event) => {
                event.preventDefault();
                setNotice(null);
                create.mutate({
                  name: newName.trim(),
                  baseUrl: newBaseUrl.trim(),
                  apiKey: newApiKey.trim() === "" ? null : newApiKey.trim(),
                  models: parseModels(newModels),
                });
              }}
              style={{ display: "flex", flexDirection: "column", gap: 12 }}
            >
              <div style={{ fontSize: 13, fontWeight: 600 }}>Novo provider</div>
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
                  gap: 12,
                }}
              >
                <FormField label="Adaptador (nome registrado em código)">
                  <select
                    value={newName}
                    onChange={(event) => setNewName(event.target.value)}
                    style={inputStyle}
                  >
                    <option value="">Selecione…</option>
                    {adapters.map((adapter) => (
                      <option key={adapter} value={adapter}>
                        {adapter}
                      </option>
                    ))}
                  </select>
                </FormField>
                <FormField label="URL base (ex.: http://ollama:11434)">
                  <input
                    value={newBaseUrl}
                    onChange={(event) => setNewBaseUrl(event.target.value)}
                    placeholder="http://ollama:11434"
                    style={inputStyle}
                    autoComplete="off"
                  />
                </FormField>
                <FormField label="Chave da API (opcional — ex.: ollama local não usa)">
                  <input
                    type="password"
                    value={newApiKey}
                    onChange={(event) => setNewApiKey(event.target.value)}
                    style={inputStyle}
                    autoComplete="new-password"
                  />
                </FormField>
                <FormField label="Modelos (separados por vírgula)">
                  <input
                    value={newModels}
                    onChange={(event) => setNewModels(event.target.value)}
                    placeholder="llama3.2, qwen"
                    style={inputStyle}
                    autoComplete="off"
                  />
                </FormField>
              </div>
              <div className="flex align-items-center gap-2 flex-wrap">
                <Button
                  type="submit"
                  variant="primary"
                  disabled={create.isPending || newName.trim() === "" || newBaseUrl.trim() === ""}
                  style={{ padding: "6px 14px" }}
                >
                  {create.isPending ? "Salvando…" : "Salvar"}
                </Button>
                <span style={{ fontSize: 11, color: "#94a3b8" }}>
                  Chave cifrada no servidor (AES-GCM) · nunca exibida de volta
                </span>
              </div>
              {create.isError && (
                <FormError
                  message={
                    mutationErrorMessage(create.error as Error) ??
                    "Não foi possível salvar o provider."
                  }
                />
              )}
            </form>
          </div>
        </Can>
      )}

      <div className="uni-panel" style={{ padding: 16 }}>
        <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 10 }}>Providers cadastrados</div>
        <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
          <thead>
            <tr style={{ textAlign: "left", color: "#64748b", fontSize: 12 }}>
              <th style={{ padding: "6px 8px" }}>Nome</th>
              <th style={{ padding: "6px 8px" }}>Estado</th>
              <th style={{ padding: "6px 8px" }}>URL base</th>
              <th style={{ padding: "6px 8px" }}>Chave</th>
              <th style={{ padding: "6px 8px" }}>Modelos</th>
              <th style={{ padding: "6px 8px" }}>Ações</th>
            </tr>
          </thead>
          <tbody>
            {providers.map((provider) => {
              const testResult = testResults[provider.id];
              const modelResult = modelResults[provider.id];
              return (
              <tr key={provider.id} style={{ borderTop: "1px solid #f1f5f9" }}>
                <td style={{ padding: "8px", fontWeight: 500 }}>{provider.name}</td>
                <td style={{ padding: "8px" }}>
                  {provider.active ? (
                    <StatusChip label="Ativo" background="#dcfce7" color="#166534" />
                  ) : (
                    <StatusChip label="Inativo" background="#fee2e2" color="#b91c1c" />
                  )}
                </td>
                <td style={{ padding: "8px" }}>{provider.baseUrl}</td>
                <td style={{ padding: "8px", color: "#64748b" }}>
                  {provider.apiKeySet ? provider.apiKeyMasked ?? "••••••" : "—"}
                </td>
                <td style={{ padding: "8px", color: "#64748b" }}>{provider.models.join(", ") || "—"}</td>
                <td style={{ padding: "8px" }}>
                  <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                    <Can level="administer">
                      <Button
                        type="button"
                        variant="info"
                        style={{ padding: "4px 10px", fontSize: 12 }}
                        onClick={() => startEdit(provider)}
                      >
                        Editar
                      </Button>
                      <Button
                        type="button"
                        variant="info"
                        style={{ padding: "4px 10px", fontSize: 12 }}
                        disabled={test.isPending}
                        onClick={() => test.mutate(provider.id)}
                      >
                        Testar conexão
                      </Button>
                      <Button
                        type="button"
                        variant="info"
                        style={{ padding: "4px 10px", fontSize: 12 }}
                        disabled={models.isPending}
                        onClick={() => models.mutate(provider.id)}
                      >
                        Listar modelos
                      </Button>
                      <Button
                        type="button"
                        variant={provider.active ? "info" : "primary"}
                        style={{ padding: "4px 10px", fontSize: 12 }}
                        disabled={toggleActive.isPending}
                        onClick={() => toggleActive.mutate({ id: provider.id, active: !provider.active })}
                      >
                        {provider.active ? "Desativar" : "Ativar"}
                      </Button>
                    </Can>
                  </div>
                  {testResult && (
                    <div
                      role="status"
                      style={{
                        marginTop: 6,
                        padding: "6px 8px",
                        borderRadius: 6,
                        fontSize: 12,
                        background: testResult.ok ? "#f0fdf4" : "#fef2f2",
                        color: testResult.ok ? "#15803d" : "#b91c1c",
                      }}
                    >
                      {testResult.ok
                        ? "Conexão OK (§13)."
                        : `Falha: ${testResult.error ?? "erro desconhecido"}`}
                    </div>
                  )}
                  {modelResult && (
                    <div
                      role="status"
                      style={{
                        marginTop: 6,
                        padding: "6px 8px",
                        borderRadius: 6,
                        fontSize: 12,
                        background: modelResult.error ? "#fef2f2" : "#f0fdf4",
                        color: modelResult.error ? "#b91c1c" : "#15803d",
                      }}
                    >
                      {modelResult.error
                        ? `Falha: ${modelResult.error}`
                        : `Modelos (§14): ${modelResult.models.join(", ") || "nenhum"}`}
                    </div>
                  )}
                  {editId === provider.id && (
                    <form
                      onSubmit={(event) => {
                        event.preventDefault();
                        update.mutate({
                          id: provider.id,
                          baseUrl: editBaseUrl.trim(),
                          apiKey: editApiKey.trim() === "" ? null : editApiKey.trim(),
                          models: parseModels(editModels),
                        });
                      }}
                      style={{
                        marginTop: 8,
                        padding: 10,
                        border: "1px solid #e2e8f0",
                        borderRadius: 6,
                        display: "flex",
                        flexDirection: "column",
                        gap: 8,
                      }}
                    >
                      <FormField label="URL base">
                        <input
                          value={editBaseUrl}
                          onChange={(event) => setEditBaseUrl(event.target.value)}
                          style={inputStyle}
                          autoComplete="off"
                        />
                      </FormField>
                      <FormField
                        label={
                          provider.apiKeySet
                            ? "Nova chave (deixe em branco para manter a atual)"
                            : "Chave da API"
                        }
                      >
                        <input
                          type="password"
                          value={editApiKey}
                          onChange={(event) => setEditApiKey(event.target.value)}
                          placeholder={provider.apiKeySet ? "••••••••" : ""}
                          style={inputStyle}
                          autoComplete="new-password"
                        />
                      </FormField>
                      <FormField label="Modelos (separados por vírgula)">
                        <input
                          value={editModels}
                          onChange={(event) => setEditModels(event.target.value)}
                          style={inputStyle}
                          autoComplete="off"
                        />
                      </FormField>
                      <div className="flex align-items-center gap-2">
                        <Button
                          type="submit"
                          variant="primary"
                          disabled={update.isPending}
                          style={{ padding: "5px 12px" }}
                        >
                          {update.isPending ? "Salvando…" : "Salvar"}
                        </Button>
                        <Button
                          type="button"
                          variant="info"
                          style={{ padding: "5px 12px" }}
                          onClick={() => setEditId(null)}
                        >
                          Fechar
                        </Button>
                      </div>
                    </form>
                  )}
                </td>
              </tr>
              );
            })}
            {providers.length === 0 && (
              <tr>
                <td colSpan={6} style={{ padding: 12, color: "#64748b" }}>
                  Nenhum provider cadastrado — o resolve usa a configuração por variável de ambiente.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="uni-panel" style={{ padding: 16 }}>
        <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 4 }}>
          Roteamento por capacidade
        </div>
        <div style={{ fontSize: 12, color: "#64748b", marginBottom: 10 }}>
          Sem rota, a capacidade cai no fallback de configuração (D3-A) e, sem nada, no estado “não
          configurado” (§44). Provider inativo nunca é usado pelo router.
        </div>
        <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
          <thead>
            <tr style={{ textAlign: "left", color: "#64748b", fontSize: 12 }}>
              <th style={{ padding: "6px 8px" }}>Capacidade</th>
              <th style={{ padding: "6px 8px" }}>Origem</th>
              <th style={{ padding: "6px 8px" }}>Provider / modelo</th>
              <th style={{ padding: "6px 8px" }}>Fallback</th>
              <th style={{ padding: "6px 8px" }}>Ações</th>
            </tr>
          </thead>
          <tbody>
            {capabilities.map((route) => {
              const draft = routeDrafts[route.capability] ?? {
                providerId: route.providerId ?? "",
                defaultModel: route.defaultModel ?? "",
                fallbackProviderId: route.fallbackProviderId ?? "",
                fallbackModel: route.fallbackModel ?? "",
              };
              return (
                <tr key={route.capability} style={{ borderTop: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "8px", fontWeight: 500 }}>{route.capability}</td>
                  <td style={{ padding: "8px" }}>
                    {route.providerId ? (
                      <StatusChip label="cadastro" background="#dbeafe" color="#1d4ed8" />
                    ) : (
                      <StatusChip label="config fallback" background="#f1f5f9" color="#64748b" />
                    )}
                  </td>
                  <td style={{ padding: "8px" }}>
                    {route.providerId ? (
                      <>
                        {route.providerName}{" "}
                        {route.defaultModel && (
                          <span style={{ color: "#64748b" }}>· {route.defaultModel}</span>
                        )}
                        {!route.providerActive && (
                          <span style={{ marginLeft: 6 }}>
                            <StatusChip label="inativo" background="#fee2e2" color="#b91c1c" />
                          </span>
                        )}
                      </>
                    ) : (
                      <span style={{ color: "#64748b" }}>—</span>
                    )}
                  </td>
                  <td style={{ padding: "8px", color: "#64748b" }}>
                    {route.fallbackProviderId
                      ? `${route.fallbackProviderName ?? route.fallbackProviderId}${route.fallbackModel ? ` · ${route.fallbackModel}` : ""}`
                      : "—"}
                  </td>
                  <td style={{ padding: "8px" }}>
                    <Can level="administer">
                      <div
                        style={{
                          display: "flex",
                          flexDirection: "column",
                          gap: 6,
                          minWidth: 220,
                        }}
                      >
                        <select
                          value={draft.providerId}
                          onChange={(event) =>
                            setRouteDrafts((prev) => ({
                              ...prev,
                              [route.capability]: { ...draft, providerId: event.target.value },
                            }))
                          }
                          style={inputStyle}
                        >
                          <option value="">Sem rota (config fallback)</option>
                          {providers.map((provider) => (
                            <option key={provider.id} value={provider.id}>
                              {provider.name}
                              {!provider.active ? " (inativo)" : ""}
                            </option>
                          ))}
                        </select>
                        <input
                          value={draft.defaultModel}
                          onChange={(event) =>
                            setRouteDrafts((prev) => ({
                              ...prev,
                              [route.capability]: { ...draft, defaultModel: event.target.value },
                            }))
                          }
                          placeholder="modelo default (ex.: llama3.2)"
                          style={inputStyle}
                          autoComplete="off"
                        />
                        <select
                          value={draft.fallbackProviderId}
                          onChange={(event) =>
                            setRouteDrafts((prev) => ({
                              ...prev,
                              [route.capability]: { ...draft, fallbackProviderId: event.target.value },
                            }))
                          }
                          style={inputStyle}
                        >
                          <option value="">Sem fallback</option>
                          {providers
                            .filter((provider) => provider.id !== draft.providerId)
                            .map((provider) => (
                              <option key={provider.id} value={provider.id}>
                                {provider.name}
                              </option>
                            ))}
                        </select>
                        <input
                          value={draft.fallbackModel}
                          onChange={(event) =>
                            setRouteDrafts((prev) => ({
                              ...prev,
                              [route.capability]: { ...draft, fallbackModel: event.target.value },
                            }))
                          }
                          placeholder="modelo de fallback (opcional)"
                          style={inputStyle}
                          autoComplete="off"
                        />
                        <div style={{ display: "flex", gap: 6 }}>
                          <Button
                            type="button"
                            variant="primary"
                            style={{ padding: "4px 10px", fontSize: 12 }}
                            disabled={
                              saveRoute.isPending ||
                              draft.providerId === "" ||
                              draft.defaultModel.trim() === ""
                            }
                            onClick={() =>
                              saveRoute.mutate({
                                capability: route.capability,
                                providerId: draft.providerId,
                                defaultModel: draft.defaultModel.trim(),
                                fallbackProviderId:
                                  draft.fallbackProviderId === "" ? null : draft.fallbackProviderId,
                                fallbackModel:
                                  draft.fallbackModel.trim() === "" ? null : draft.fallbackModel.trim(),
                              })
                            }
                          >
                            Salvar rota
                          </Button>
                          {route.providerId && (
                            <Button
                              type="button"
                              variant="info"
                              style={{ padding: "4px 10px", fontSize: 12 }}
                              disabled={clearRoute.isPending}
                              onClick={() => clearRoute.mutate(route.capability)}
                            >
                              Limpar
                            </Button>
                          )}
                        </div>
                      </div>
                    </Can>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
