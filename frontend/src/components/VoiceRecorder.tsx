"use client";

import { useEffect, useRef, useState } from "react";
import api from "@/src/lib/axios";
import { toast } from "react-hot-toast";

type Props = {
  onSuccess?: () => void;
};

export default function VoiceRecorder({ onSuccess }: Props) {
  const [isRecording, setIsRecording] = useState(false);
  const [mediaRecorder, setMediaRecorder] = useState<MediaRecorder | null>(
    null,
  );
  const [audioUrl, setAudioUrl] = useState<string | null>(null);
  const [audioBlob, setAudioBlob] = useState<Blob | null>(null);
  const [seconds, setSeconds] = useState(0);
  const timerRef = useRef<number | null>(null);
  const chunksRef = useRef<Blob[]>([]);

  // Timer sadece unmount'ta temizlensin; mediaRecorder değişince cleanup tetiklenmesin (yoksa süre 00:00'da kalır)
  useEffect(() => {
    return () => {
      if (timerRef.current !== null) {
        window.clearInterval(timerRef.current);
        timerRef.current = null;
      }
    };
  }, []);

  useEffect(() => {
    return () => {
      mediaRecorder?.stream.getTracks().forEach((t) => t.stop());
    };
  }, [mediaRecorder]);

  const startTimer = () => {
    if (timerRef.current !== null) return;
    setSeconds(0);
    timerRef.current = window.setInterval(() => {
      setSeconds((s) => s + 1);
    }, 1000);
  };

  const stopTimer = () => {
    if (timerRef.current !== null) {
      window.clearInterval(timerRef.current);
      timerRef.current = null;
    }
  };

  const handleStart = async () => {
    if (!navigator.mediaDevices?.getUserMedia) {
      toast.error("Tarayıcınız mikrofon erişimini desteklemiyor.");
      return;
    }

    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const recorder = new MediaRecorder(stream);
      chunksRef.current = [];

      recorder.ondataavailable = (e) => {
        if (e.data.size > 0) {
          chunksRef.current.push(e.data);
        }
      };

      recorder.onstop = () => {
        stopTimer();
        const blob = new Blob(chunksRef.current, { type: "audio/webm" });
        if (blob.size === 0) {
          toast.error("Boş bir kayıt alındı. Lütfen tekrar deneyin.");
          return;
        }
        setAudioBlob(blob);
        setAudioUrl(URL.createObjectURL(blob));
      };

      recorder.start();
      setMediaRecorder(recorder);
      setIsRecording(true);
      startTimer();
    } catch (err) {
      console.error(err);
      toast.error(
        "Mikrofon izni verilmedi veya bir hata oluştu. Lütfen tarayıcı izinlerini kontrol edin.",
      );
    }
  };

  const handleStop = () => {
    if (!mediaRecorder) return;
    mediaRecorder.stop();
    mediaRecorder.stream.getTracks().forEach((t) => t.stop());
    setIsRecording(false);
  };

  const handleSave = async () => {
    if (!audioBlob) {
      toast.error("Önce bir ses kaydı oluşturmalısınız.");
      return;
    }

    const formData = new FormData();
    formData.append("file", audioBlob, "recording.webm");
    formData.append("title", "Sesli Not");

    try {
      await api.post("/Audio/note-from-audio", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      toast.success("Ses kaydından not oluşturuldu.");
      setAudioBlob(null);
      setAudioUrl(null);
      if (onSuccess) onSuccess();
    } catch (err) {
      const msg =
        err?.response?.data?.message ??
        err?.message ??
        "Ses kaydından not oluşturulurken bir hata oluştu.";
      toast.error(msg);
    }
  };

  const minutes = Math.floor(seconds / 60)
    .toString()
    .padStart(2, "0");
  const secs = (seconds % 60).toString().padStart(2, "0");

  return (
    <div className="space-y-3 text-xs text-slate-100">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={isRecording ? handleStop : handleStart}
          className={`inline-flex items-center gap-2 rounded-full px-4 py-1.5 text-xs font-medium transition ${
            isRecording
              ? "bg-red-600 text-white hover:bg-red-500"
              : "bg-emerald-600 text-white hover:bg-emerald-500"
          }`}
        >
          <span
            className={`h-2.5 w-2.5 rounded-full ${
              isRecording
                ? "bg-red-300 animate-pulse shadow-[0_0_0_4px_rgba(248,113,113,0.5)]"
                : "bg-emerald-300"
            }`}
          />
          {isRecording ? "Kaydı Durdur" : "Kaydı Başlat"}
        </button>
        <span className="font-mono text-[11px] text-slate-300">
          Süre: {minutes}:{secs}
        </span>
      </div>

      {isRecording && (
        <p className="text-[11px] text-red-300 flex items-center gap-1">
          <span className="h-1.5 w-1.5 rounded-full bg-red-400 animate-pulse" />
          Kayıt devam ediyor...
        </p>
      )}

      {audioUrl && (
        <div className="space-y-2">
          <p className="text-[11px] text-slate-400">Kayıt önizlemesi:</p>
          <audio src={audioUrl} controls className="w-full" />
          <button
            type="button"
            onClick={handleSave}
            className="inline-flex items-center rounded-xl bg-indigo-500 px-4 py-2 text-xs font-semibold text-white hover:bg-indigo-400"
          >
            Not Olarak Kaydet
          </button>
        </div>
      )}
    </div>
  );
}

