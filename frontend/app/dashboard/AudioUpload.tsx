"use client";

import { useState } from "react";
import api from "@/src/lib/axios";
import { toast } from "react-hot-toast";

type Props = {
  onSuccess?: () => void;
};

export default function AudioUpload({ onSuccess }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);

  const handleUpload = async () => {
    if (!file) {
      toast.error("Lütfen bir ses dosyası seçin.");
      return;
    }

    setIsUploading(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("title", file.name.replace(/\.[^/.]+$/, "") || "Sesli Not");

      await api.post("/Audio/note-from-audio", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });

      toast.success("Ses dosyasından not oluşturuldu.");
      setFile(null);
      if (onSuccess) onSuccess();
    } catch (err) {
      const msg =
        err?.response?.data?.message ??
        err?.message ??
        "Ses dosyasından not oluşturulurken bir hata oluştu.";
      toast.error(msg);
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <div className="space-y-2 text-xs text-slate-100">
      <input
        type="file"
        accept="audio/*"
        onChange={(e) => setFile(e.target.files?.[0] ?? null)}
        className="w-full text-xs file:mr-2 file:rounded-full file:border-0 file:bg-emerald-500 file:px-3 file:py-1.5 file:text-xs file:font-medium file:text-white hover:file:bg-emerald-400"
      />
      {file && (
        <p className="text-[11px] text-slate-400">Seçilen: {file.name}</p>
      )}
      <button
        type="button"
        onClick={handleUpload}
        disabled={isUploading}
        className="inline-flex items-center rounded-xl bg-emerald-600 px-4 py-2 text-xs font-semibold text-white hover:bg-emerald-500 disabled:opacity-60"
      >
        {isUploading ? "Gönderiliyor..." : "Not Olarak Kaydet"}
      </button>
    </div>
  );
}

