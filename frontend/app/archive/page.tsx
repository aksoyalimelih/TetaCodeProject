"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/src/lib/axios";
import Link from "next/link";
import { toast } from "react-hot-toast";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

type NoteDto = {
  id: number;
  title: string;
  description: string;
  filePath?: string | null;
  createdAt: string;
};

async function fetchArchivedNotes(): Promise<NoteDto[]> {
  const response = await api.get<NoteDto[]>("/Notes/archive");
  return response.data;
}

export default function ArchivePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const assetBaseUrl =
    process.env.NEXT_PUBLIC_ASSET_BASE_URL ?? "http://localhost:5047";

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      router.push("/login");
    }
  }, [router]);

  const { data: notes, isLoading } = useQuery({
    queryKey: ["notes-archive"],
    queryFn: fetchArchivedNotes,
  });

  const restoreMutation = useMutation({
    mutationFn: async (id: number) => {
      await api.post(`/Notes/${id}/restore`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notes"] });
      queryClient.invalidateQueries({ queryKey: ["notes-archive"] });
      toast.success("Not başarıyla geri yüklendi.");
    },
    onError: () => {
      toast.error("Not geri yüklenirken hata oluştu.");
    },
  });

  const hardDeleteMutation = useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`/Notes/${id}/hard`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notes-archive"] });
      toast.success("Not kalıcı olarak silindi.");
    },
    onError: () => {
      toast.error("Not silinirken hata oluştu.");
    },
  });

  const handleLogout = () => {
    localStorage.removeItem("token");
    router.push("/login");
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-50">
      <header className="border-b border-slate-800 bg-slate-950/80 backdrop-blur sticky top-0 z-10">
        <div className="mx-auto max-w-5xl flex items-center justify-between px-4 py-3">
          <div>
            <h1 className="text-lg font-semibold tracking-tight">
              Arşivlenmiş Notlar
            </h1>
            <div className="mt-2 inline-flex rounded-full bg-slate-900/80 p-1 text-xs">
              <Link
                href="/dashboard"
                className="px-4 py-1.5 rounded-full text-slate-400 hover:text-slate-100 hover:bg-slate-800 transition"
              >
                Notlarım
              </Link>
              <Link
                href="/archive"
                className="px-4 py-1.5 rounded-full bg-slate-800 text-slate-50"
              >
                Arşivim
              </Link>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="text-xs px-3 py-1.5 rounded-full border border-slate-700 hover:bg-slate-800 transition"
          >
            Çıkış Yap
          </button>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-4 py-6 space-y-6">
        <section className="space-y-3">
          <h2 className="text-sm font-medium text-slate-100">
            Arşivlenmiş Notlar
          </h2>

          {isLoading ? (
            <p className="text-xs text-slate-400">Yükleniyor...</p>
          ) : !notes || notes.length === 0 ? (
            <p className="text-xs text-slate-500">
              Arşivde hiç not yok. Dashboard üzerinden notları arşive
              gönderebilirsiniz.
            </p>
          ) : (
            <div className="grid gap-4 md:grid-cols-2">
              {notes.map((note) => (
                <article
                  key={note.id}
                  className="flex flex-col justify-between rounded-2xl border border-slate-800 bg-slate-900/70 p-4 shadow-lg shadow-slate-950/40"
                >
                  <div className="space-y-1">
                    <h3 className="text-sm font-semibold text-slate-50">
                      {note.title}
                    </h3>
                    <p className="text-xs text-slate-300">
                      {note.description}
                    </p>
                    <p className="text-[11px] text-slate-500">
                      Oluşturulma:{" "}
                      {new Date(note.createdAt).toLocaleString("tr-TR")}
                    </p>
                  </div>
                  <div className="mt-3 flex flex-wrap gap-2 text-[11px]">
                    <button
                      onClick={() => restoreMutation.mutate(note.id)}
                      className="rounded-full border border-emerald-500/60 px-3 py-1 text-emerald-300 hover:bg-emerald-500/10 transition"
                    >
                      Geri Yükle
                    </button>
                    <button
                      onClick={() => hardDeleteMutation.mutate(note.id)}
                      className="rounded-full border border-red-500/60 px-3 py-1 text-red-300 hover:bg-red-500/10 transition"
                    >
                      Kalıcı Sil
                    </button>
                    {note.filePath && (
                      <a
                        href={`${assetBaseUrl}/${note.filePath}`}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="rounded-full border border-slate-600 px-3 py-1 text-slate-200 hover:bg-slate-800 transition"
                      >
                        Dosyayı Görüntüle
                      </a>
                    )}
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
}

