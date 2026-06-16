export interface FormErrors {
  [key: string]: Array<{
    message: string;
  }>;
}

export interface FormValues {
  [key: string]: any;
}

export interface FormResolverOptions {
  values: FormValues;
}

export interface FormSubmitEvent {
  valid: boolean;
  values: FormValues;
} 